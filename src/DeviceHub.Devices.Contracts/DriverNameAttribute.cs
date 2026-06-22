namespace DeviceHub.Devices.Contracts;

[AttributeUsage(AttributeTargets.Class)]
public class DriverNameAttribute : Attribute
{
    public string Name { get; }
    public DriverNameAttribute(string name) => Name = name;
}
