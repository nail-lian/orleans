using System;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Runtime.MembershipService;

namespace Orleans.Hosting
{
    /// <summary>
    /// Extensions for <see cref="ISiloHostBuilder"/> instances.
    /// </summary>
    public static class CoreHostingExtensions
    {
        /// <summary>
        /// Configure the container to use Orleans.
        /// </summary>
        /// <param name="builder">The host builder.</param>
        /// <returns>The host builder.</returns>
        public static ISiloHostBuilder ConfigureDefaults(this ISiloHostBuilder builder)
        {
            return builder.ConfigureServices((context, services) =>
            {
                if (!context.Properties.ContainsKey("OrleansServicesAdded"))
                {
                    services.PostConfigure<SiloOptions>(
                        options => options.SiloName =
                            options.SiloName
                            ?? context.HostingEnvironment.ApplicationName
                            ?? $"Silo_{Guid.NewGuid().ToString("N").Substring(0, 5)}");

                    services.TryAddSingleton<Silo>();
                    DefaultSiloServices.AddDefaultServices(context, services);

                    context.Properties.Add("OrleansServicesAdded", true);
                }
            });
        }

        /// <summary>
        /// Configures the silo to use development-only clustering and listen on localhost.
        /// </summary>
        /// <param name="builder">The silo builder.</param>
        /// <param name="siloPort">The silo port.</param>
        /// <param name="gatewayPort">The gateway port.</param>
        /// <param name="primarySiloEndpoint">
        /// The endpoint of the primary silo, or <see langword="null"/> to use this silo as the primary.
        /// </param>
        /// <param name="serviceId">The service id.</param>
        /// <param name="clusterId">The cluster id.</param>
        /// <returns>The silo builder.</returns>
        public static ISiloHostBuilder UseLocalhostClustering(
            this ISiloHostBuilder builder,
            int siloPort = EndpointOptions.DEFAULT_SILO_PORT,
            int gatewayPort = EndpointOptions.DEFAULT_GATEWAY_PORT,
            IPEndPoint primarySiloEndpoint = null,
            string serviceId = ClusterOptions.DevelopmentServiceId,
            string clusterId = ClusterOptions.DevelopmentClusterId)
        {
            builder.Configure<EndpointOptions>(options =>
            {
                options.AdvertisedIPAddress = IPAddress.Loopback;
                options.SiloPort = siloPort;
                options.GatewayPort = gatewayPort;
            });

            builder.UseDevelopmentClustering(primarySiloEndpoint ?? new IPEndPoint(IPAddress.Loopback, siloPort));
            builder.Configure<ClusterMembershipOptions>(options => options.ExpectedClusterSize = 1);
            builder.ConfigureServices(services =>
            {
                // If the caller did not override service id or cluster id, configure default values as a fallback.
                if (string.Equals(serviceId, ClusterOptions.DevelopmentServiceId) && string.Equals(clusterId, ClusterOptions.DevelopmentClusterId))
                {
                    services.PostConfigure<ClusterOptions>(options =>
                    {
                        if (string.IsNullOrWhiteSpace(options.ClusterId)) options.ClusterId = ClusterOptions.DevelopmentClusterId;
                        if (string.IsNullOrWhiteSpace(options.ServiceId)) options.ServiceId = ClusterOptions.DevelopmentServiceId;
                    });
                }
                else
                {
                    services.Configure<ClusterOptions>(options =>
                    {
                        options.ServiceId = serviceId;
                        options.ClusterId = clusterId;
                    });
                }
            });

            return builder;
        }

        /// <summary>
        /// Configures the silo to use development-only clustering.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="primarySiloEndpoint">
        /// The endpoint of the primary silo, or <see langword="null"/> to use this silo as the primary.
        /// </param>
        /// <returns>The silo builder.</returns>
        public static ISiloHostBuilder UseDevelopmentClustering(this ISiloHostBuilder builder, IPEndPoint primarySiloEndpoint)
        {
            return builder.UseDevelopmentClustering(options => options.PrimarySiloEndpoint = primarySiloEndpoint);
        }

        /// <summary>
        /// Configures the silo to use development-only clustering.
        /// </summary>
        public static ISiloHostBuilder UseDevelopmentClustering(
            this ISiloHostBuilder builder,
            Action<DevelopmentClusterMembershipOptions> configureOptions)
        {
            return builder.UseDevelopmentClustering(options => options.Configure(configureOptions));
        }

        /// <summary>
        /// Configures the silo to use development-only clustering.
        /// </summary>
        public static ISiloHostBuilder UseDevelopmentClustering(
            this ISiloHostBuilder builder,
            Action<OptionsBuilder<DevelopmentClusterMembershipOptions>> configureOptions)
        {
            return builder.ConfigureServices(
                services =>
                {
                    configureOptions?.Invoke(services.AddOptions<DevelopmentClusterMembershipOptions>());
                    services.ConfigureFormatter<DevelopmentClusterMembershipOptions>();
                    services
                        .AddSingleton<GrainBasedMembershipTable>()
                        .AddFromExisting<IMembershipTable, GrainBasedMembershipTable>();
                });
        }
    }
}