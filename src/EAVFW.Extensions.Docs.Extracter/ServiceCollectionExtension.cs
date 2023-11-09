using Microsoft.Extensions.DependencyInjection;

namespace EAVFW.Extensions.Docs.Extracter
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddDocument(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IDocumentLogic, DocumentLogic>();

            return serviceCollection;
        }
    }
}
