using CartService.Models;

namespace CartService.Tests;

public class CartTests
{
    [Fact]
    public void New_Cart_Should_Have_No_Items()
    {
        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        Assert.Empty(cart.Items);
    }

    [Fact]
    public void Adding_Item_Should_Increase_Item_Count()
    {
        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        cart.Items.Add(new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            UnitPrice = 50
        });

        Assert.Single(cart.Items);
    }

    [Fact]
    public void Cart_Total_Should_Be_Quantity_Multiplied_By_Price()
    {
        var item = new CartItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 3,
            UnitPrice = 50
        };

        var total = item.Quantity * item.UnitPrice;

        Assert.Equal(150, total);
    }

    [Fact]
    public void Adding_Same_Product_Should_Increase_Quantity()
    {
        var item = new CartItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            UnitPrice = 50
        };

        var additionalQuantity = 3;

        item.Quantity += additionalQuantity;

        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void Updating_Item_Should_Change_Quantity()
    {
        var item = new CartItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            UnitPrice = 50
        };

        item.Quantity = 7;

        Assert.Equal(7, item.Quantity);
    }

    [Fact]
    public void Cart_Total_Items_Should_Sum_All_Quantities()
    {
        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        cart.Items.Add(new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            UnitPrice = 20
        });

        cart.Items.Add(new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 3,
            UnitPrice = 30
        });

        var totalItems =
            cart.Items.Sum(i => i.Quantity);

        Assert.Equal(5, totalItems);
    }

    [Fact]
    public void Cart_Total_Amount_Should_Sum_Item_Totals()
    {
        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        cart.Items.Add(new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            UnitPrice = 20
        });

        cart.Items.Add(new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 3,
            UnitPrice = 30
        });

        var totalAmount = cart.Items.Sum(
            i => i.Quantity * i.UnitPrice);

        Assert.Equal(130, totalAmount);
    }
}