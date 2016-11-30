namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Features;
    using ObjectBuilder;
    using Pipeline;
    using Routing;
    using Transport;

    class PublishSubscribeFeature : Feature
    {
        public PublishSubscribeFeature()
        {
            DependsOn<RoutingFeature>(); // depend on routing since provider implementations may rely on routing APIs.
            EnableByDefault();
        }

        protected internal override void Setup(FeatureConfigurationContext context)
        {
            var canReceive = !context.Settings.GetOrDefault<bool>("Endpoint.SendOnly");

            IPublishSubscribeProvider publishSubscribeProvider;
            if (!context.Settings.TryGet(out publishSubscribeProvider))
            {
                publishSubscribeProvider = new NoPubSub();
            }

            var routerFactory = publishSubscribeProvider.GetRouter(context);
            context.Container.ConfigureComponent(routerFactory, DependencyLifecycle.SingleInstance);

            context.Pipeline.Register(typeof(PublishRouterBehavior), "Determines how the published messages should be routed");

            if (canReceive)
            {
                var subscriptionManagerFactory = publishSubscribeProvider.GetSubscriptionManager(context);
                context.Container.ConfigureComponent(subscriptionManagerFactory, DependencyLifecycle.SingleInstance);

                context.Pipeline.Register(typeof(NativeSubscribeTerminator), "Requests the transport to subscribe to a given message type");
                context.Pipeline.Register(typeof(NativeUnsubscribeTerminator), "Requests the transport to unsubscribe to a given message type");
            }
        }

        class NoPubSub : IPublishSubscribeProvider, IPublishRouter, IManageSubscriptions
        {
            public Func<IBuilder, IPublishRouter> GetRouter(FeatureConfigurationContext context)
            {
                return builder => this;
            }

            public Func<IBuilder, IManageSubscriptions> GetSubscriptionManager(FeatureConfigurationContext context)
            {
                return builder => this;
            }

            public Task<RoutingStrategy[]> GetRoutingStrategies(IOutgoingPublishContext context)
            {
                throw new InvalidOperationException($"No publish/subscribe mechanism has been configured, therefore no events can be published. Ensure {nameof(MessageDrivenSubscriptions)} are enabled or that you provide a custom publish/subscribe mechanism.");
            }

            public Task Subscribe(Type eventType, ContextBag context)
            {
                throw new InvalidOperationException($"Cannot subscribe to events without a publish/subscribe mechanism. Ensure {nameof(MessageDrivenSubscriptions)} are enabled or that you provide a custom publish/subscribe mechanism.");
            }

            public Task Unsubscribe(Type eventType, ContextBag context)
            {
                throw new InvalidOperationException($"Cannot unsubscribe to events without a publish/subscribe mechanism. Ensure {nameof(MessageDrivenSubscriptions)} are enabled or that you provide a custom publish/subscribe mechanism.");
            }
        }
    }
}