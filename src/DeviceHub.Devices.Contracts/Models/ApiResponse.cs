namespace DeviceHub.Devices.Contracts;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }

    public ApiResponse() { }

    public ApiResponse(T data)
    {
        Success = true;
        Data = data;
    }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
}
