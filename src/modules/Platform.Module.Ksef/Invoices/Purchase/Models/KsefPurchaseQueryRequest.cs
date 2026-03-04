using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Invoices.Purchase.Models;

/// <summary>
/// Wejściowy model API platformy do zapytania o faktury zakupowe.
/// Zakres dat jest obowiązkowy.
/// Filtry są opcjonalne.
/// </summary>
public sealed class KsefPurchaseQueryRequest
{
    [Required]
    public KsefEnvironment Env { get; init; }

    /// <summary>
    /// Zakres dat jest obowiązkowy (od).
    /// </summary>
    [Required]
    public DateTime DateFrom { get; init; }

    /// <summary>
    /// Zakres dat jest obowiązkowy (do).
    /// </summary>
    [Required]
    public DateTime DateTo { get; init; }

    /// <summary>
    /// Opcjonalne filtry (NIP, status, typ dokumentu).
    /// Na razie tylko kontrakt – mapowanie do MF zrobimy w kolejnym kroku.
    /// </summary>
    public FiltersSection? Filters { get; init; }

    public sealed class FiltersSection
    {
        /// <summary>
        /// Lista NIP (opcjonalnie).
        /// </summary>
        public List<string>? Nips { get; init; }

        /// <summary>
        /// Statusy dokumentów (opcjonalnie).
        /// </summary>
        public List<string>? Statuses { get; init; }

        /// <summary>
        /// Typy dokumentów (opcjonalnie).
        /// </summary>
        public List<string>? DocumentTypes { get; init; }
    }
}
