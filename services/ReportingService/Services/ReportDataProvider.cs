using System.Globalization;
using System.Text.Json;

namespace ReportingService.Services;

public sealed class ReportDataProvider(IConfiguration configuration, IHttpClientFactory clients, IHttpContextAccessor httpContextAccessor)
{
    public static readonly string[] SupportedReports = ["sales", "revenue", "orders", "products", "inventory", "customers", "payments", "delivery", "coupons"];

    public async Task<JsonElement> GetAsync(string reportType, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var type = reportType.ToLowerInvariant();
        if (!SupportedReports.Contains(type)) throw new NotSupportedException("This report is not supported.");
        var query = BuildDateQuery(from, to);
        var document = type switch
        {
            "sales" or "orders" or "products" => await FetchAllPages("Order", "api/admin/orders", "orders", query, cancellationToken),
            "revenue" or "payments" => await FetchAllPages("Payment", "api/admin/payments/transactions", "transactions", query, cancellationToken),
            "inventory" => await Fetch("Inventory", "api/admin/inventory/stock?page=1&pageSize=100", cancellationToken),
            "customers" => await Fetch("Auth", "api/admin/customers?page=1&pageSize=100", cancellationToken),
            "delivery" => await Fetch("Delivery", "api/admin/delivery/shipments?page=1&pageSize=100", cancellationToken),
            "coupons" => await Fetch("Promotion", "api/admin/promotions/coupons", cancellationToken),
            _ => throw new NotSupportedException("This report is not supported.")
        };
        using (document)
        {
            var root = document.RootElement.Clone();
            return type switch
            {
                "sales" => AggregateSales(root),
                "orders" => root,
                "revenue" => AggregateRevenue(root),
                "products" => AggregateProducts(root),
                _ => root
            };
        }
    }

    private async Task<JsonDocument> Fetch(string service, string path, CancellationToken cancellationToken)
    {
        var address = configuration[$"ServiceUrls:{service}"];
        if (!Uri.TryCreate(address, UriKind.Absolute, out var baseAddress)) throw new HttpRequestException($"{service} report source is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseAddress, path));
        if (httpContextAccessor.HttpContext?.Request.Headers.Authorization is { } authorization
            && !string.IsNullOrWhiteSpace(authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization.ToString());
        }

        using var response = await clients.CreateClient("reports").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"{service} report source returned {(int)response.StatusCode}.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private async Task<JsonDocument> FetchAllPages(string service, string path, string collection, string query, CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var rows = new List<JsonElement>();
        JsonElement firstPage = default;
        var totalItems = 0;

        for (var page = 1; ; page++)
        {
            using var document = await Fetch(service, $"{path}?page={page}&pageSize={pageSize}{query}", cancellationToken);
            var root = document.RootElement;
            if (page == 1)
            {
                firstPage = root.Clone();
            }

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(collection, out var pageRows)
                || pageRows.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var currentRows = pageRows.EnumerateArray().Select(row => row.Clone()).ToList();
            rows.AddRange(currentRows);
            totalItems = root.TryGetProperty("totalItems", out var total)
                && total.TryGetInt32(out var parsedTotal)
                    ? parsedTotal
                    : rows.Count;

            if (rows.Count >= totalItems || currentRows.Count == 0)
            {
                break;
            }
        }

        if (firstPage.ValueKind != JsonValueKind.Object || !firstPage.TryGetProperty(collection, out _))
        {
            return JsonDocument.Parse(firstPage.ValueKind == JsonValueKind.Undefined ? "{}" : firstPage.GetRawText());
        }

        var result = System.Text.Json.Nodes.JsonNode.Parse(firstPage.GetRawText())!.AsObject();
        var allRows = new System.Text.Json.Nodes.JsonArray();
        foreach (var row in rows)
        {
            allRows.Add(System.Text.Json.Nodes.JsonNode.Parse(row.GetRawText()));
        }

        result[collection] = allRows;
        result["totalItems"] = totalItems;

        return JsonDocument.Parse(result.ToJsonString());
    }

    private static string BuildDateQuery(DateTime? from, DateTime? to)
    {
        var query = "";
        if (from.HasValue) query += "&from=" + Uri.EscapeDataString(from.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        if (to.HasValue) query += "&to=" + Uri.EscapeDataString(to.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        return query;
    }

    private static JsonElement AggregateSales(JsonElement root)
    {
        var orders = root.TryGetProperty("orders", out var array) && array.ValueKind == JsonValueKind.Array ? array.EnumerateArray().ToList() : [];
        var valid = orders.Where(x => !String(x, "status").Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)).ToList();
        var days = valid.GroupBy(x => Date(x, "createdAt").Date).OrderBy(x => x.Key).Select(g => new { date = g.Key, orderCount = g.Count(), grossOrderValue = g.Sum(x => Decimal(x, "totalAmount")) });
        return JsonSerializer.SerializeToElement(new { metric = "gross order value; excludes cancelled orders", orderCount = valid.Count, grossOrderValue = valid.Sum(x => Decimal(x, "totalAmount")), days });
    }

    private static JsonElement AggregateRevenue(JsonElement root)
    {
        var rows = root.TryGetProperty("transactions", out var array) && array.ValueKind == JsonValueKind.Array ? array.EnumerateArray().ToList() : [];
        var completed = rows.Where(x => String(x, "status").Equals("COMPLETED", StringComparison.OrdinalIgnoreCase)).ToList();
        return JsonSerializer.SerializeToElement(new { metric = "completed payment transactions", transactionCount = completed.Count, totalAmount = completed.Sum(x => Decimal(x, "amount")), transactions = rows });
    }

    private static JsonElement AggregateProducts(JsonElement root)
    {
        var orders = root.TryGetProperty("orders", out var array) && array.ValueKind == JsonValueKind.Array ? array.EnumerateArray().ToList() : [];
        var lines = orders.Where(x => !String(x, "status").Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
            .Where(x => x.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            .SelectMany(x => x.GetProperty("items").EnumerateArray()).ToList();
        var products = lines.GroupBy(x => String(x, "productId")).Select(g => new { productId = g.Key, units = g.Sum(x => (int)Decimal(x, "quantity")), grossSales = g.Sum(x => Decimal(x, "totalPrice")) }).OrderByDescending(x => x.grossSales);
        return JsonSerializer.SerializeToElement(products);
    }

    private static string String(JsonElement item, string property) => item.TryGetProperty(property, out var value) ? value.ToString() : "";
    private static decimal Decimal(JsonElement item, string property) => item.TryGetProperty(property, out var value) && value.TryGetDecimal(out var number) ? number : 0;
    private static DateTime Date(JsonElement item, string property) => item.TryGetProperty(property, out var value) && value.TryGetDateTime(out var date) ? date : DateTime.MinValue;
}
