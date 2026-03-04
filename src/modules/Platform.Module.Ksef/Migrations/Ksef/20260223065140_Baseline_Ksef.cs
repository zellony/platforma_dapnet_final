using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Module.Ksef.Migrations.Ksef
{
    /// <inheritdoc />
    public partial class Baseline_Ksef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Schema must exist
            migrationBuilder.EnsureSchema(name: "app_core");

            // Create KSeF tables in an idempotent way:
            // - On existing DB (tables already exist): no error
            // - On new DB: tables are created
            migrationBuilder.Sql(@"
create table if not exists app_core.ksef_user_credentials (
  id uuid not null,
  user_id uuid not null,
  environment text not null,
  active_type text not null,
  cert_crt bytea null,
  cert_key bytea null,
  cert_key_password_enc bytea null,
  cert_fingerprint_sha256 text null,
  token text null,
  token_valid_to timestamptz null,
  refresh_token text null,
  refresh_token_valid_to timestamptz null,
  created_at_utc timestamptz not null,
  updated_at_utc timestamptz not null,
  constraint pk_ksef_user_credentials primary key (id)
);

create index if not exists ix_ksef_user_credentials_user_env
  on app_core.ksef_user_credentials (user_id, environment);

create table if not exists app_core.ksef_sync_state (
  environment text not null,
  context_nip text not null,
  last_permanent_storage_hwm_date timestamptz null,
  last_attempt_at_utc timestamptz null,
  last_success_at_utc timestamptz null,
  last_error text null,
  updated_at_utc timestamptz not null,
  constraint pk_ksef_sync_state primary key (environment, context_nip)
);

create table if not exists app_core.ksef_sync_lock (
  environment text not null,
  context_nip text not null,
  locked_until_utc timestamptz not null,
  locked_by_user_id uuid not null,
  constraint pk_ksef_sync_lock primary key (environment, context_nip)
);

create table if not exists app_core.ksef_rate_limit_state (
  user_id uuid not null,
  environment text not null,
  blocked_until_utc timestamptz null,
  last_429_at_utc timestamptz null,
  retry_after_seconds integer null,
  last_details text null,
  updated_at_utc timestamptz not null,
  constraint pk_ksef_rate_limit_state primary key (user_id, environment)
);

create table if not exists app_core.ksef_purchase_invoices (
  id uuid not null,
  environment text not null,
  context_nip text not null,
  ksef_number text not null,
  invoice_number text null,
  issue_date date null,
  permanent_storage_date date null,
  seller_nip text null,
  seller_name text null,
  buyer_identifier_type text null,
  buyer_identifier_value text null,
  buyer_name text null,
  net_amount numeric null,
  vat_amount numeric null,
  gross_amount numeric null,
  currency text null,
  has_attachment boolean not null,
  invoice_hash text null,
  updated_at_utc timestamptz not null,
  constraint pk_ksef_purchase_invoices primary key (id)
);

create index if not exists ix_ksef_purchase_invoices_env_ctx
  on app_core.ksef_purchase_invoices (environment, context_nip);

create index if not exists ix_ksef_purchase_invoices_ksef_number
  on app_core.ksef_purchase_invoices (ksef_number);

create table if not exists app_core.ksef_auto_sync_settings (
  environment text not null,
  context_nip text not null,
  enabled boolean not null,
  interval_minutes integer not null,
  updated_by_user_id uuid null,
  updated_at_utc timestamptz not null,
  constraint pk_ksef_auto_sync_settings primary key (environment, context_nip)
);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Safe rollback
            migrationBuilder.Sql(@"
drop table if exists app_core.ksef_auto_sync_settings;
drop table if exists app_core.ksef_purchase_invoices;
drop table if exists app_core.ksef_rate_limit_state;
drop table if exists app_core.ksef_sync_lock;
drop table if exists app_core.ksef_sync_state;
drop table if exists app_core.ksef_user_credentials;
");
        }
    }
}