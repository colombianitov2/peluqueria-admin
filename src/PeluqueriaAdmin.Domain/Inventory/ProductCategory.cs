namespace PeluqueriaAdmin.Domain.Inventory;

public enum ProductCategory
{
    OtherProductForSale = 1,
    LocalSupply = 2,
    CustomerCourtesy = 3,
    OtherLocalProduct = 4,
    FoodOrDrinkForSale = 5,
    Cleaning = 6,

    ProductForSale = OtherProductForSale,

    MandatorySupply = LocalSupply,

    OptionalCustomerSupply = CustomerCourtesy,

    DurableEquipment = OtherLocalProduct,
}
