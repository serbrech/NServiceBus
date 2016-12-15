namespace NServiceBus.AcceptanceTests.Sagas
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_polymorphic_saga_starts_saga : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_store_state_of_both_handlers()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(e => e
                    .When(s => s.SendLocal(new StartChildMessage
                    {
                        SomeId = Guid.NewGuid(),
                    })))
                .Done(c => c.ReceivedFollowUpMessages >= 2)
                .Run();

            Assert.AreEqual(2, ctx.ReceivedFollowUpMessages);
            Assert.IsTrue(ctx.InvokedBaseHandler);
            Assert.IsTrue(ctx.InvokedChildHandler);
        }

        public class Context : ScenarioContext
        {
            public int ReceivedFollowUpMessages;
            public bool InvokedBaseHandler { get; set; }
            public bool InvokedChildHandler { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(s => s.Recoverability().Immediate(x => x.NumberOfRetries(0)));
            }

            public class SagaData : ContainSagaData
            {
                public Guid SomeId { get; set; }
            }

            class MySaga : Saga<SagaData>,
                IAmStartedByMessages<StartBaseMessage>,
                IAmStartedByMessages<StartChildMessage>,
                IHandleMessages<FollowUpMessage>
            {
                public Context TestContext { get; set; }

                public async Task Handle(StartBaseMessage message, IMessageHandlerContext context)
                {
                    TestContext.InvokedBaseHandler = true;
                    await context.SendLocal(new FollowUpMessage()
                    {
                        SomeId = Data.SomeId
                    });
                }

                public async Task Handle(StartChildMessage message, IMessageHandlerContext context)
                {
                    TestContext.InvokedChildHandler = true;
                    await context.SendLocal(new FollowUpMessage()
                    {
                        SomeId = Data.SomeId
                    });
                }

                public Task Handle(FollowUpMessage message, IMessageHandlerContext context)
                {
                    Interlocked.Increment(ref TestContext.ReceivedFollowUpMessages);
                    return Task.FromResult(0);
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper)
                {
                    mapper.ConfigureMapping<StartBaseMessage>(m => m.SomeId).ToSaga(s => s.SomeId);
                    mapper.ConfigureMapping<StartChildMessage>(m => m.SomeId).ToSaga(s => s.SomeId);
                    mapper.ConfigureMapping<FollowUpMessage>(m => m.SomeId).ToSaga(s => s.SomeId);
                }
            }
        }

        public class StartBaseMessage : ICommand
        {
            public Guid SomeId { get; set; }
            public Guid SomeOtherId { get; set; }
        }

        public class StartChildMessage : StartBaseMessage
        {
        }

        public class FollowUpMessage : ICommand
        {
            public Guid SomeId { get; set; }
        }
    }


}