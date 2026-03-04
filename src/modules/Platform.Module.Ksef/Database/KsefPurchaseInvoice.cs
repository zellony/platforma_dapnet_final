using System;

namespace Platform.Api.Modules.KSeF.Entities;

public sealed class KsefPurchaseInvoice
{
    public Guid Id { get; set; }

    public KsefEnvironment Environment { get; set; }
    public string ContextNip { get; set; } = default!;

    public string KsefNumber { get; set; } = default!;
    public string? InvoiceNumber { get; set; }

    public DateOnly? IssueDate { get; set; }
    public DateOnly? PermanentStorageDate { get; set; }

    public string? SellerNip { get; set; }
    public string? SellerName { get; set; }

    public string? BuyerIdentifierType { get; set; }
    public string? BuyerIdentifierValue { get; set; }
    public string? BuyerName { get; set; }

    public decimal? NetAmount { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? GrossAmount { get; set; }
    public string? Currency { get; set; }

    public bool HasAttachment { get; set; }

    public string? InvoiceHash { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
