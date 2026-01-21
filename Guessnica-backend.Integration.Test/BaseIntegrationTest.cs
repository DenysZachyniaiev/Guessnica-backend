using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Xunit;

namespace Guessnica_backend.Integration.Test;

public abstract class BaseIntegrationTest : IClassFixture<IntegrationTestGuessnicaFactory>, IDisposable
{
    private readonly IServiceScope _scope;
    protected readonly ISender Sender;
    
    protected BaseIntegrationTest(IntegrationTestGuessnicaFactory factory)
    {
        _scope = factory.Services.CreateScope();
        Sender = _scope.ServiceProvider.GetRequiredService<ISender>();
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }
}