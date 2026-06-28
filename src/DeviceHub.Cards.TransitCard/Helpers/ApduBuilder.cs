namespace DeviceHub.Cards.TransitCard.Helpers;

public static class ApduBuilder
{
    public const string DefaultHostChallenge = "A0A1A2A3A4A5A6A7";
    public const int AmountHexLength = 8;

    public static string SelectTransitAid() => "00A4040008A00000000386980701";
    public static string GetBalance() => "805C000204";

    public static string ReadBinary(byte sfi = 0, int offset = 0, int le = 0x08)
        => $"00B0{sfi:X2}{offset:X2}{le:X2}";

    public static string GetCardNumber() => ReadBinary(0, 0, 0x08);

    public static string ReadRecord(byte sfi, byte recordNo, int le = 0x14)
        => $"00B2{recordNo:X2}{4 | (sfi << 3):X2}{le:X2}";

    public static string GetTransactionLog() => ReadRecord(1, 1, 0x14);

    public static string InitRecharge(string hostChallenge)
        => $"8050000008{hostChallenge}";

    public static string CreditForLoad(string amountHex)
        => $"8054000008{amountHex}0000000000000000";

    public static string InitConsume(int dealflag, int keyindex, string amountHex, string termainno)
        => $"805001{dealflag:X2}0B{keyindex:X2}{amountHex}{termainno}0F";

    public static string InitCappConsume(int dealflag, int keyindex, string amountHex, string termainno)
        => $"805003{dealflag:X2}0B{keyindex:X2}{amountHex}{termainno}0F";

    public static string DebitForPurchase(int termdealno, string dealtime, string mac1)
        => $"805401000F{termdealno:X8}{dealtime}{mac1}08";
}
