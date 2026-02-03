namespace RallyAPI.Delivery.Domain.Enums;

public enum DeliveryFailureReason
{
    None = 0,
    CustomerUnavailable = 1,
    WrongAddress = 2,
    CustomerRefused = 3,
    RestaurantClosed = 4,
    FoodDamaged = 5,
    RiderEmergency = 6,
    NoRidersAvailable = 7,
    ThirdPartyFailed = 8,
    SystemError = 9,
    Other = 99
}