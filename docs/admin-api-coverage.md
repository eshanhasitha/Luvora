# Admin API coverage

Admin endpoints are routed through ApiGateway under `/api/admin/...`. The gateway validates JWT issuer, audience, signature, and lifetime, and applies the `AdminOnly` role policy to admin groups. `/api/admin/auth/login` is the anonymous exception and accepts only active admin accounts. In Docker, AuthService and the gateway must use the same `JWT_KEY`. `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD` seed an initial admin only when set.

## Service ownership and implemented API groups

| Checklist group | Owner | Current coverage and important gaps |
|---|---|---|
| Auth, staff, roles | AuthService | Login, profile read/update, password change, staff list/create/read/status, limited role assignment. No refresh/logout, MFA, password reset workflow, sessions, or custom permission/role catalog. |
| Products, categories | ProductService | Product CRUD/status/images and categories CRUD/status are partly present. Product variants, related products, import/export/bulk operations and image reorder/removal are missing. Category hierarchy/image/reorder and a separate Brand entity/API are missing; product brand remains a string. |
| Inventory | InventoryService | Summary, stock list/read, low/out-of-stock, stock adjustments and movement history. Adjustments persist before/after movement records; no warehouse, transfer, reservation, or inventory export/report workflow. |
| Orders, returns | OrderService | List/detail/items, cancellation and constrained status updates. No order timeline/notes/confirm/fulfill/ship workflows or returns model. |
| Customers | AuthService | List/detail and account suspend/reactivate. Password hashes are omitted. Profile update, order/address/activity views, notes, password-reset delivery and export are not complete. |
| Payments, refunds | PaymentService | Summary and transaction list/detail/order lookup against existing payment data. No provider retry, refund workflow/idempotency, reconciliation or finance reports. |
| Delivery | DeliveryService | Summary, shipments list/detail/create/status using current delivery model. Personnel assignment, tracking events, zones, rates and failed-delivery workflows are not modeled. |
| Promotions, coupons, campaigns, banners | PromotionService (new) | Persistent CRUD/status for promotions, coupons, campaigns and banners, coupon usage view, banner ordering/image URL, with date/discount/limit validation. Coupon redemption integration is not connected, so usage counts are not yet populated by checkout. Banner image endpoint accepts a URL; it is not a file upload. |
| Reviews | ReviewService | List/detail/summary and approve/hide/reject. Report resolution and official replies are not represented by the current review model. |
| Support, complaints | SupportService (new) | Persistent ticket and complaint list/read/create/status, ticket assignment and messages. Internal messages are distinguished from customer-visible messages. No customer-facing support integration/identity federation. |
| Reports, dashboard | ReportingService (new) | Dashboard reads and sales/revenue/order/product/inventory/customer/payment/delivery/coupon reports, asynchronous JSON export jobs and temporary download links. Refund reports return not implemented because refunds are not modeled. Aggregations query current service APIs and are limited by their current data/page sizes. |
| Store settings, email templates | ConfigurationService (new) | Persistent general/currency/tax/shipping/payment-status/notifications/homepage settings and template CRUD. Secret values are rejected and provider secrets are not returned. Test email returns 501 until a mail provider is configured. |
| Notifications | NotificationService | List/read/unread operations. Admin preferences and template ownership are split/not fully implemented. |
| Audit, system | AuditService (new) | Append-only admin audit ingestion from gateway; searchable records, security events, staff activity, export history, service health, safe storage summary and integration status/test. Queue metrics and failed-job management return 501 until centralized queue/job stores are configured. |

## New service wiring

PromotionService, SupportService, ReportingService, ConfigurationService, and AuditService are separate .NET projects in `services/Luvora.sln`. Each has its own PostgreSQL database and EF Core migration. Docker Compose creates these databases for existing Postgres volumes, starts the services, and routes their admin API paths through the gateway. Reporting calls owning service APIs for aggregates; it does not query their databases directly.

## Known checklist gaps

The checklist describes workflows beyond the data currently present in this repository. Missing domain models/integrations remain missing rather than being represented by fake success responses: MFA and reset delivery, granular RBAC, catalog variants/brands/hierarchy, returns, payment refunds/reconciliation, warehouse transfers, delivery personnel/zones/rates, checkout coupon redemption, review reports/replies, customer notes/export, and centralized queue/job metrics. Several new routes deliberately return 501 where the required provider or store is not configured.
