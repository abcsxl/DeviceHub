namespace DeviceHub.Devices.Contracts;

public record IdCardInfo(
    string Name,
    string Gender,
    string Ethnicity,
    string BirthDate,
    string Address,
    string IdNumber,
    string IssuingAuthority,
    string ValidFrom,
    string ValidTo
);
