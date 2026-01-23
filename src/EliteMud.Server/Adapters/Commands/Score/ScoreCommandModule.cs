using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Score;

internal sealed class ScoreCommandModule : ICommandModule
{

    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new ScoreCommandHandler(serviceProvider.GetRequiredService<EliteMud.Application.Commands.Score.ScoreHandler>());
    }
}
