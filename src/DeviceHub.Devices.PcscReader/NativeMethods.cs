using System.Runtime.InteropServices;

namespace DeviceHub.Devices.PcscReader;

internal static class NativeMethods
{
    private const string WinSCard = "winscard.dll";
    private const string UnixSCard = "libpcsclite.so.1";

    [DllImport(WinSCard, EntryPoint = "SCardEstablishContext", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int SCardEstablishContextWin(uint dwScope, nint pvReserved1, nint pvReserved2, out nint phContext);

    [DllImport(UnixSCard, EntryPoint = "SCardEstablishContext")]
    private static extern int SCardEstablishContextUnix(uint dwScope, nint pvReserved1, nint pvReserved2, out nint phContext);

    public static int EstablishContext(uint dwScope, out nint phContext)
    {
        if (OperatingSystem.IsWindows())
            return SCardEstablishContextWin(dwScope, nint.Zero, nint.Zero, out phContext);
        else
            return SCardEstablishContextUnix(dwScope, nint.Zero, nint.Zero, out phContext);
    }

    [DllImport(WinSCard, EntryPoint = "SCardReleaseContext", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int SCardReleaseContextWin(nint hContext);

    [DllImport(UnixSCard, EntryPoint = "SCardReleaseContext")]
    private static extern int SCardReleaseContextUnix(nint hContext);

    public static int ReleaseContext(nint hContext)
    {
        if (OperatingSystem.IsWindows())
            return SCardReleaseContextWin(hContext);
        else
            return SCardReleaseContextUnix(hContext);
    }

    [DllImport(WinSCard, EntryPoint = "SCardListReadersW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern int SCardListReadersWin(nint hContext, byte[]? mszGroups, byte[]? mszReaders, ref uint pcchReaders);

    [DllImport(UnixSCard, EntryPoint = "SCardListReaders")]
    private static extern int SCardListReadersUnix(nint hContext, byte[]? mszGroups, byte[]? mszReaders, ref uint pcchReaders);

    public static int ListReaders(nint hContext, byte[]? mszReaders, ref uint pcchReaders)
    {
        if (OperatingSystem.IsWindows())
            return SCardListReadersWin(hContext, null, mszReaders, ref pcchReaders);
        else
            return SCardListReadersUnix(hContext, null, mszReaders, ref pcchReaders);
    }

    [DllImport(WinSCard, EntryPoint = "SCardConnectW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern int SCardConnectWin(nint hContext, string szReader, uint dwShareMode, uint dwPreferredProtocols, out nint phCard, out uint pdwActiveProtocol);

    [DllImport(UnixSCard, EntryPoint = "SCardConnect", CharSet = CharSet.Ansi)]
    private static extern int SCardConnectUnix(nint hContext, string szReader, uint dwShareMode, uint dwPreferredProtocols, out nint phCard, out uint pdwActiveProtocol);

    public static int Connect(nint hContext, string szReader, uint dwShareMode, uint dwPreferredProtocols, out nint phCard, out uint pdwActiveProtocol)
    {
        if (OperatingSystem.IsWindows())
            return SCardConnectWin(hContext, szReader, dwShareMode, dwPreferredProtocols, out phCard, out pdwActiveProtocol);
        else
            return SCardConnectUnix(hContext, szReader, dwShareMode, dwPreferredProtocols, out phCard, out pdwActiveProtocol);
    }

    [DllImport(WinSCard, EntryPoint = "SCardDisconnect", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int SCardDisconnectWin(nint hCard, uint dwDisposition);

    [DllImport(UnixSCard, EntryPoint = "SCardDisconnect")]
    private static extern int SCardDisconnectUnix(nint hCard, uint dwDisposition);

    public static int Disconnect(nint hCard, uint dwDisposition)
    {
        if (OperatingSystem.IsWindows())
            return SCardDisconnectWin(hCard, dwDisposition);
        else
            return SCardDisconnectUnix(hCard, dwDisposition);
    }

    [DllImport(WinSCard, EntryPoint = "SCardTransmit", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int SCardTransmitWin(nint hCard, ref SCardIORequest pioSendPci, byte[] pbSendBuffer, uint cbSendLength, ref SCardIORequest pioRecvPci, byte[] pbRecvBuffer, ref uint pcbRecvLength);

    [DllImport(UnixSCard, EntryPoint = "SCardTransmit", CharSet = CharSet.Ansi)]
    private static extern int SCardTransmitUnix(nint hCard, ref SCardIORequest pioSendPci, byte[] pbSendBuffer, uint cbSendLength, ref SCardIORequest pioRecvPci, byte[] pbRecvBuffer, ref uint pcbRecvLength);

    public static int Transmit(nint hCard, ref SCardIORequest pioSendPci, byte[] pbSendBuffer, uint cbSendLength, ref SCardIORequest pioRecvPci, byte[] pbRecvBuffer, ref uint pcbRecvLength)
    {
        if (OperatingSystem.IsWindows())
            return SCardTransmitWin(hCard, ref pioSendPci, pbSendBuffer, cbSendLength, ref pioRecvPci, pbRecvBuffer, ref pcbRecvLength);
        else
            return SCardTransmitUnix(hCard, ref pioSendPci, pbSendBuffer, cbSendLength, ref pioRecvPci, pbRecvBuffer, ref pcbRecvLength);
    }

    [DllImport(WinSCard, EntryPoint = "SCardStatusW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern int SCardStatusWin(nint hCard, byte[]? szReaderName, ref uint pcchReaderLen, out uint pdwState, out uint pdwProtocol, byte[]? pbAtr, ref uint pcbAtrLen);

    [DllImport(UnixSCard, EntryPoint = "SCardStatus", CharSet = CharSet.Ansi)]
    private static extern int SCardStatusUnix(nint hCard, byte[]? szReaderName, ref uint pcchReaderLen, out uint pdwState, out uint pdwProtocol, byte[]? pbAtr, ref uint pcbAtrLen);

    public static int Status(nint hCard, byte[]? szReaderName, ref uint pcchReaderLen, out uint pdwState, out uint pdwProtocol, byte[]? pbAtr, ref uint pcbAtrLen)
    {
        if (OperatingSystem.IsWindows())
            return SCardStatusWin(hCard, szReaderName, ref pcchReaderLen, out pdwState, out pdwProtocol, pbAtr, ref pcbAtrLen);
        else
            return SCardStatusUnix(hCard, szReaderName, ref pcchReaderLen, out pdwState, out pdwProtocol, pbAtr, ref pcbAtrLen);
    }

    [DllImport(WinSCard, EntryPoint = "SCardGetAttrib", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int SCardGetAttribWin(nint hCard, uint dwAttribId, byte[]? pbAttr, ref uint pcbAttrLen);

    [DllImport(UnixSCard, EntryPoint = "SCardGetAttrib")]
    private static extern int SCardGetAttribUnix(nint hCard, uint dwAttribId, byte[]? pbAttr, ref uint pcbAttrLen);

    public static int GetAttrib(nint hCard, uint dwAttribId, byte[]? pbAttr, ref uint pcbAttrLen)
    {
        if (OperatingSystem.IsWindows())
            return SCardGetAttribWin(hCard, dwAttribId, pbAttr, ref pcbAttrLen);
        else
            return SCardGetAttribUnix(hCard, dwAttribId, pbAttr, ref pcbAttrLen);
    }

    [DllImport(WinSCard, EntryPoint = "SCardCancel", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int SCardCancelWin(nint hContext);

    [DllImport(UnixSCard, EntryPoint = "SCardCancel")]
    private static extern int SCardCancelUnix(nint hContext);

    public static int Cancel(nint hContext)
    {
        if (OperatingSystem.IsWindows())
            return SCardCancelWin(hContext);
        else
            return SCardCancelUnix(hContext);
    }

    public const uint SCardScopeSystem = 2;
    public const uint SCardShareShared = 1;
    public const uint SCardProtocolTx = 3;
    public const uint SCardLeaveCard = 0;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SCardIORequest
{
    public uint Protocol;
    public uint Length;
}
