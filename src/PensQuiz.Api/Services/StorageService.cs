using System.Net.Http.Headers;
using System.Text.Json;

namespace PensQuiz.Api.Services;

public interface IStorageService
{
    Task<string?> UploadProfileImage(Guid userId, string fileName, string contentType, Stream fileStream);
    Task<string?> UploadQuizCover(Guid quizId, bool isPublic, string fileName, string contentType, Stream fileStream);
    Task<string?> UploadQuestionImage(Guid questionId, bool isPublic, string fileName, string contentType, Stream fileStream);
    Task<bool> DeleteImage(string bucket, string path);
    Task<string?> GetSignedUrl(string bucket, string path, int expirySeconds = 3600);
    Task<bool> MoveQuizAssets(Guid quizId, bool fromPublicToPrivate);
}

public sealed class StorageService(IConfiguration configuration, ILogger<StorageService> logger) : IStorageService
{
    private readonly string _supabaseUrl = configuration["SUPABASE_URL"] ?? "";
    private readonly string _serviceRoleKey = NonEmpty(configuration["SUPABASE_SERVICE_ROLE_KEY"], configuration["SUPABASE_ANON_KEY"]) ?? "";
    
    private static string? NonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri($"{_supabaseUrl.TrimEnd('/')}/storage/v1/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        client.DefaultRequestHeaders.Add("apikey", _serviceRoleKey);
        return client;
    }

    public async Task<string?> UploadProfileImage(Guid userId, string fileName, string contentType, Stream fileStream)
    {
        var path = $"{userId}/{Guid.NewGuid()}_{fileName}";
        return await UploadToBucket("profile-images", path, contentType, fileStream);
    }

    public async Task<string?> UploadQuizCover(Guid quizId, bool isPublic, string fileName, string contentType, Stream fileStream)
    {
        var bucket = isPublic ? "public-assets" : "private-assets";
        var path = $"quizzes/{quizId}/cover_{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        return await UploadToBucket(bucket, path, contentType, fileStream);
    }

    public async Task<string?> UploadQuestionImage(Guid questionId, bool isPublic, string fileName, string contentType, Stream fileStream)
    {
        var bucket = isPublic ? "public-assets" : "private-assets";
        var path = $"questions/{questionId}/{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        return await UploadToBucket(bucket, path, contentType, fileStream);
    }

    private async Task<string?> UploadToBucket(string bucket, string path, string contentType, Stream fileStream)
    {
        using var client = CreateClient();
        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var response = await client.PostAsync($"object/{bucket}/{path}", content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            logger.LogError("Supabase Storage Upload Failed: {Status} {Error}", response.StatusCode, error);
            return null;
        }

        // For public buckets, return the public URL. For private, return the path.
        if (bucket.Contains("public") || bucket == "profile-images")
        {
            return $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{bucket}/{path}";
        }
        return path; // Store path for private
    }

    public async Task<bool> DeleteImage(string bucket, string path)
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync($"object/{bucket}/{path}");
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> GetSignedUrl(string bucket, string path, int expirySeconds = 3600)
    {
        using var client = CreateClient();
        var payload = new { expiresIn = expirySeconds };
        var response = await client.PostAsJsonAsync($"object/sign/{bucket}/{path}", payload);
        
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (result.TryGetProperty("signedURL", out var urlProp))
        {
            return urlProp.GetString();
        }
        return null;
    }

    public async Task<bool> MoveQuizAssets(Guid quizId, bool fromPublicToPrivate)
    {
        var sourceBucket = fromPublicToPrivate ? "public-assets" : "private-assets";
        var destBucket = fromPublicToPrivate ? "private-assets" : "public-assets";
        
        using var client = CreateClient();
        
        // List objects in quizzes/{quizId}/
        var listResponse = await client.PostAsJsonAsync($"object/list/{sourceBucket}", new { prefix = $"quizzes/{quizId}/" });
        if (!listResponse.IsSuccessStatusCode) return false;

        var objects = await listResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        if (objects == null) return true;

        foreach (var obj in objects)
        {
            if (obj.TryGetProperty("name", out var nameProp))
            {
                var name = nameProp.GetString();
                if (string.IsNullOrEmpty(name)) continue;

                var sourcePath = $"quizzes/{quizId}/{name}";
                // Supabase doesn't have a cross-bucket move API. We must copy + delete.
                // Copy API: POST /object/copy
                var copyPayload = new { 
                    fromBucket = sourceBucket, 
                    toBucket = destBucket, 
                    sourceKey = sourcePath, 
                    destinationKey = sourcePath 
                };
                
                var copyResponse = await client.PostAsJsonAsync("object/copy", copyPayload);
                if (copyResponse.IsSuccessStatusCode)
                {
                    await client.DeleteAsync($"object/{sourceBucket}/{sourcePath}");
                }
            }
        }

        return true;
    }
}
