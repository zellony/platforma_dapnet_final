namespace Platform.Api.Modules.KSeF.Options;

public sealed class KsefOptions
{
    public string Environment { get; set; } = "PRD"; // TE/TR/PRD (domyślne)

    public KsefEnvOptions Environments { get; set; } = new();

    public sealed class KsefEnvOptions
    {
        public KsefSingleEnvOptions TE { get; set; } = new();
        public KsefSingleEnvOptions TR { get; set; } = new();
        public KsefSingleEnvOptions PRD { get; set; } = new();
    }

    public sealed class KsefSingleEnvOptions
    {
        public string ApiBaseUrl { get; set; } = default!; // np. https://api.ksef.mf.gov.pl/api/v2
        public string ContextNip { get; set; } = default!;
        public string SubjectIdentifierType { get; set; } = "certificateSubject"; // lub certificateFingerprint
    }
}
