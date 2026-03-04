namespace Platform.Api.Infrastructure;

public sealed class RuntimeInstanceContext
{
    public string InstanceId { get; }

    public RuntimeInstanceContext(string instanceId)
    {
        InstanceId = instanceId;
    }
}
