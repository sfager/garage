using Garage.Domain;

namespace Garage.Web.Api.Contracts;

public class DocumentUploadFormRequest
{
    public string Title { get; set; } = string.Empty;

    public DocumentType Type { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public IFormFile? File { get; set; }
}
