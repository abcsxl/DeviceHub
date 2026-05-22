namespace DeviceHub.Cards.TransitCard.Constants;

public static class ApduCommands
{
    public const string SelectTransitAid = "00A4040008A00000000386980701";
    public const string GetBalance = "805C000204";
    public const string GetCardNumber = "00B0000008";
    public const string GetTransactionLog = "00B2010C14";
    public const string DefaultHostChallenge = "A0A1A2A3A4A5A6A7";
    public const string InitRecharge = "8050000008";
    public const string CreditForLoad = "8054000008";
    public const int AmountHexLength = 8;
}
