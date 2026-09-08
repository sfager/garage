using Garage.Application.Documents;
using Garage.Domain.Common;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class DocumentsController(DocumentService docs) : ControllerBase
{
    [HttpGet("vehicles/{vehicleId:guid}/documents")]
    public async Task<ActionResult<IReadOnlyList<DocumentCardResponse>>> ListFilesAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await docs.ListFilesAsync(vehicleId, cancellationToken);
            return Ok(result.Select(ToResponse).ToList());
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpGet("vehicles/{vehicleId:guid}/documents/receipts")]
    public async Task<ActionResult<IReadOnlyList<ReceiptGroupResponse>>> ListReceiptGroupsAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            var groups = await docs.ListReceiptGroupsAsync(vehicleId, cancellationToken);
            var result = groups.Select(g => new ReceiptGroupResponse(
                g.ServiceRecordId,
                g.Date,
                g.Summary,
                g.Receipts.Select(ToResponse).ToList())).ToList();
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpGet("documents/expiring")]
    public async Task<ActionResult<IReadOnlyList<ExpiringDocumentResponse>>> ListExpiringAsync(CancellationToken cancellationToken)
    {
        var expiring = await docs.ListExpiringAsync(cancellationToken);
        var result = expiring.Select(x => new ExpiringDocumentResponse(ToResponse(x.Document), x.VehicleNickname)).ToList();
        return Ok(result);
    }

    [HttpGet("documents/{documentId:guid}")]
    public async Task<ActionResult<DocumentCardResponse>> GetAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await docs.GetAsync(documentId, cancellationToken);
        return document is null ? NotFound() : Ok(ToResponse(document));
    }

    [HttpPut("documents/{documentId:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid documentId, [FromBody] DocumentUploadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await docs.UpdateAsync(documentId, request.Title, request.Type, request.ExpiresOn, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            await docs.DeleteAsync(documentId, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("documents/{documentId:guid}/expiry-reminders")]
    public async Task<IActionResult> CreateExpiryReminderAsync(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            var reminder = await docs.CreateExpiryReminderAsync(documentId, cancellationToken: cancellationToken);
            return Ok(new { reminder.Id, reminder.DueDate });
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("vehicles/{vehicleId:guid}/documents")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<DocumentCardResponse>> UploadAsync(
        Guid vehicleId,
        [FromForm] DocumentUploadFormRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new ApiErrorResponse("Choose a file to upload."));
        }

        if (request.File.Length > 20 * 1024 * 1024)
        {
            return BadRequest(new ApiErrorResponse("That file is larger than 20 MB."));
        }

        try
        {
            var upload = new DocumentUploadRequest
            {
                Title = request.Title,
                Type = request.Type,
                ExpiresOn = request.ExpiresOn
            };

            await using var stream = request.File.OpenReadStream();
            var document = await docs.UploadAsync(
                vehicleId,
                upload,
                stream,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                cancellationToken);

            var card = await docs.GetAsync(document.Id, cancellationToken);
            return card is null ? NotFound() : Ok(ToResponse(card));
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    private DocumentCardResponse ToResponse(DocumentCard card)
    {
        var url = docs.GetUrl(card.StoragePath);
        return new DocumentCardResponse(
            card.Id,
            card.VehicleId,
            card.Type,
            card.Title,
            card.FileName,
            card.ContentType,
            card.StoragePath,
            card.SizeBytes,
            card.ExpiresOn,
            card.DaysUntilExpiry,
            card.IsImage,
            url,
            card.IsExpiringSoon,
            card.HasExpired,
            card.NeedsAttention,
            card.ExpiryDescription);
    }
}
