using System.Net.Http;
using SecureProxy.Dal.Domain;

namespace SecureProxy.Bl.Security;

public interface IUpstreamAuthProfileProvider
{
    void Apply(HttpRequestMessage request, Destination destination);
}
