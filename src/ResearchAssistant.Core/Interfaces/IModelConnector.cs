using System.Threading.Tasks;
using Systen.Threading;

namespace ResearchAssistant.Core.Interfaces;

public interface IModelConnector
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
