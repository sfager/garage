using System.Net;
using System.Net.Http.Json;
using Garage.Application.Documents;
using Garage.Domain.Common;
using Garage.Web.Api.Contracts;

namespace Garage.Web.Services.Api;

public class DocumentApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<DocumentCardResponse>> ListFilesAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var docs = await http.GetFromJsonAsync<List<DocumentCardResponse>>($"api/vehicles/{vehicleId}/documents", cancellationToken);
        return docs ?? [];
    }

    public async Task<IReadOnlyList<ReceiptGroupResponse>> ListReceiptGroupsAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var groups = await http.GetFromJsonAsync<List<ReceiptGroupResponse>>($"api/vehicles/{vehicleId}/documents/receipts", cancellationToken);
        return groups ?? [];
    }

    public async Task<IReadOnlyList<ExpiringDocumentResponse>> ListExpiringAsync(CancellationToken cancellationToken = default)
    {
        var expiring = await http.GetFromJsonAsync<List<ExpiringDocumentResponse>>("api/documents/expiring", cancellationToken);
        return expiring ?? [];
    }

    public async Task<DocumentCardResponse?> GetAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/documents/{documentId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DocumentCardResponse>(cancellationToken);
    }

    public async Task UpdateAsync(Guid documentId, DocumentUploadRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync($"api/documents/{documentId}", request, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/documents/{documentId}", cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<DateOnly?> CreateExpiryReminderAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"api/documents/{documentId}/expiry-reminders", null, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<ReminderResult>(cancellationToken);
        return payload?.DueDate;
    }

    public async Task<DocumentCardResponse> UploadAsync(
        Guid vehicleId,
        DocumentUploadRequest request,
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(request.Title), nameof(DocumentUploadFormRequest.Title));
        form.Add(new StringContent(request.Type.ToString()), nameof(DocumentUploadFormRequest.Type));

        if (request.ExpiresOn is { } expires)
        {
            form.Add(new StringContent(expires.ToString("yyyy-MM-dd")), nameof(DocumentUploadFormRequest.ExpiresOn));
        }

        using var fileContent = new StreamContent(stream);
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        form.Add(fileContent, nameof(DocumentUploadFormRequest.File), fileName);

        using var response = await http.PostAsync($"api/vehicles/{vehicleId}/documents", form, cancellationToken);
        await ApiClientErrors.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<DocumentCardResponse>(cancellationToken)
            ?? throw new DomainException("Could not read uploaded document.");
    }

    private sealed record ReminderResult(Guid Id, DateOnly? DueDate);
}
