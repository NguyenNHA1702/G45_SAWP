using Core.Constants;

namespace Core.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public string? Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success", string? code = null) =>
        new() { Success = true, Message = message, Code = code, Data = data };

    public static ApiResponse<T> Ok(T data, SystemMessage sysMsg) =>
        new() { Success = true, Code = sysMsg.Code, Message = sysMsg.Content, Data = data };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null, string? code = null) =>
        new() { Success = false, Message = message, Code = code, Errors = errors };

    public static ApiResponse<T> Fail(string message, string? code) =>
        new() { Success = false, Message = message, Code = code };

    public static ApiResponse<T> Fail(SystemMessage sysMsg, List<string>? errors = null) =>
        new() { Success = false, Code = sysMsg.Code, Message = sysMsg.Content, Errors = errors };
}
