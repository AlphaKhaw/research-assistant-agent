using System.Threading;
using System.Threading.Tasks;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Core.Interfaces;

public interface IModelConnector
{
    Task<LlmResponse> SendPromptAsync(
        string prompt,
        PromptOptions? promptOptions = null,
        CancellationToken cancellationToken = default
    );
}
