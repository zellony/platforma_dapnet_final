using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Database;

public sealed class KsefPurchaseInvoiceConfig : IEntityTypeConfiguration<KsefPurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<KsefPurchaseInvoice> b)
    {
        b.ToTable("ksef_purchase_invoices", "app_core");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.Environment)
            .HasColumnName("environment")
            .HasConversion<string>();

        b.Property(x => x.ContextNip).HasColumnName("context_nip");

        b.Property(x => x.KsefNumber).HasColumnName("ksef_number");
        b.Property(x => x.InvoiceNumber).HasColumnName("invoice_number");

        b.Property(x => x.IssueDate).HasColumnName("issue_date");
        b.Property(x => x.PermanentStorageDate).HasColumnName("permanent_storage_date");

        b.Property(x => x.SellerNip).HasColumnName("seller_nip");
        b.Property(x => x.SellerName).HasColumnName("seller_name");

        b.Property(x => x.BuyerIdentifierType).HasColumnName("buyer_identifier_type");
        b.Property(x => x.BuyerIdentifierValue).HasColumnName("buyer_identifier_value");
        b.Property(x => x.BuyerName).HasColumnName("buyer_name");

        b.Property(x => x.NetAmount).HasColumnName("net_amount");
        b.Property(x => x.VatAmount).HasColumnName("vat_amount");
        b.Property(x => x.GrossAmount).HasColumnName("gross_amount");
        b.Property(x => x.Currency).HasColumnName("currency");

        b.Property(x => x.HasAttachment).HasColumnName("has_attachment");

        b.Property(x => x.InvoiceHash).HasColumnName("invoice_hash");

        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        b.HasIndex(x => new { x.Environment, x.ContextNip }).HasDatabaseName("ix_ksef_purchase_invoices_env_ctx");
        b.HasIndex(x => x.KsefNumber).HasDatabaseName("ix_ksef_purchase_invoices_ksef_number");
    }
}
