using System.Reflection;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Abstractions.Websocket;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace AttendanceService.GraphQL;

public class GraphQLClientFactory {
    private readonly ILogger<GraphQLClientFactory> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public GraphQLClientFactory(
            ILogger<GraphQLClientFactory> logger,
            IHttpClientFactory httpClientFactory) {
        this._logger = logger;
        this._httpClientFactory = httpClientFactory;
    }

    public TImplementation CreateImplementation<TService, TImplementation>( 
            IServiceProvider serviceProvider,
            string? httpClientName = null,
            IGraphQLWebsocketJsonSerializer? serializer = null,
            Action<GraphQLHttpClientOptions>? configure = null) 
        where TService: class
        where TImplementation: class, TService
    {
        GraphQLHttpClient graphQLClient = CreateGraphQLClient(
                serviceProvider, httpClientName, serializer, configure);

        TImplementation? implementationInstance =
            CreateServiceInstance<TImplementation>(serviceProvider, graphQLClient);

        return implementationInstance;
    }

    private GraphQLHttpClient CreateGraphQLClient(
            IServiceProvider serviceProvider,
            string? httpClientName,
            IGraphQLWebsocketJsonSerializer? serializer = null,
            Action<GraphQLHttpClientOptions>? configure = null) {
        HttpClient httpClient;
        if (httpClientName is null)
        {
            httpClient = this._httpClientFactory.CreateClient();
        }
        else
        {
            httpClient = this._httpClientFactory.CreateClient(httpClientName);
        }

        GraphQLHttpClientOptions options = new GraphQLHttpClientOptions();
        if (configure is not null)
        {
            configure(options);
        }

        if (serializer is null)
        {
            serializer = new SystemTextJsonSerializer();
        }

        GraphQLHttpClient graphQLClient =
            new GraphQLHttpClient(options, serializer, httpClient);
        return graphQLClient;
    }

    private TImplementation CreateServiceInstance<TImplementation>(
            IServiceProvider serviceProvider,
            GraphQLHttpClient graphQLClient) 
        where TImplementation : class {
        Type implementationType = typeof(TImplementation);
        ConstructorInfo[] constructors = implementationType.GetConstructors();
        if (constructors.Length != 1)
        {
            this._logger.LogError("{0} must have exactly one constructor.", nameof(TImplementation));
            throw new ArgumentException(
                $"{nameof(TImplementation)} must have exactly one constructor",
                nameof(TImplementation));
        }

        ConstructorInfo constructor = constructors[0];
        ParameterInfo[] parameters = constructor.GetParameters();
        List<object> parameterInstances = new List<object>();
        foreach (ParameterInfo parameter in parameters)
        {
            Type paramType = parameter.ParameterType;
            if (paramType == typeof(IGraphQLClient)
                    || paramType == typeof(GraphQLHttpClient))
            {
                parameterInstances.Add(graphQLClient);
            }
            else
            {
                object instance = serviceProvider.GetRequiredService(paramType);
                parameterInstances.Add(instance);
            }
        }

        TImplementation? implementationInstance = (TImplementation?)Activator.CreateInstance(
            implementationType, parameterInstances.ToArray());

        if (implementationInstance is null)
        {
            this._logger.LogError("{0} could not be instaniated.", nameof(TImplementation));
            throw new Exception($"{nameof(TImplementation)} could not be instaniated.");
        }

        return implementationInstance;
    }
}

public static class GraphQLClientFactoryExtensions {
    public static void AddGraphQLClient<TService, TImplementation>(
            this IServiceCollection services,
            string? httpClientName = null,
            IGraphQLWebsocketJsonSerializer? serializer = null,
            Action<GraphQLHttpClientOptions>? configure = null) 
        where TService: class
        where TImplementation: class, TService {
        services.AddScoped<TService, TImplementation>(serviceProvider => {
            GraphQLClientFactory factory = 
                serviceProvider.GetRequiredService<GraphQLClientFactory>();

            return factory.CreateImplementation<TService, TImplementation>(
                    serviceProvider, httpClientName, serializer, configure);
    }); 
}
}

