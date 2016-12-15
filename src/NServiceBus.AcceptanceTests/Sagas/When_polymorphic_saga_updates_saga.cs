namespace NServiceBus.AcceptanceTests.Sagas
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Features;
    using NUnit.Framework;

    public class When_polymorphic_saga_updates_saga : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_store_all_changes()
        {
            var context = await Scenario.Define<TestContext>()
                .WithEndpoint<MyEndpoint>(e => e
                    .When(s => s.SendLocal(new StartSagaMessage() {MessageId = Guid.NewGuid()})))
                .Done(ctx => ctx.HandledTimeoutMessage >= 1)
                .Run();

            Assert.AreEqual(1, context.HandledStartMessage);
            Assert.AreEqual(1, context.HandledTimeoutMessage);

            Assert.IsTrue(context.StoredBaseHandlerValue);
            Assert.IsTrue(context.StoredChildHandlerValue);
        }

        class TestContext : ScenarioContext
        {
            public int HandledStartMessage;
            public int HandledTimeoutMessage;

            public bool StoredBaseHandlerValue { get; set; }
            public bool StoredChildHandlerValue { get; set; }
        }

        class MyEndpoint : EndpointConfigurationBuilder
        {
            public MyEndpoint()
            {
                EndpointSetup<DefaultServer>(s => s.EnableFeature<TimeoutManager>());

            }

            class SagaData : ContainSagaData
            {
                public Guid SagaId { get; set; }
                public bool BaseHandlerValue { get; set; }
                public bool ChildHandlerValue { get; set; }
            }

            class MySaga : Saga<SagaData>,
                IAmStartedByMessages<StartSagaMessage>,
                IHandleMessages<BaseUpdateSagaMessage>,
                IHandleMessages<ChildUpdateSagaMessage>,
                IHandleTimeouts<TimeoutMessage>
            {
                public TestContext TestContext { get; set; }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper)
                {
                    mapper.ConfigureMapping<StartSagaMessage>(m => m.MessageId).ToSaga(s => s.SagaId);
                    mapper.ConfigureMapping<BaseUpdateSagaMessage>(m => m.MessageId).ToSaga(s => s.SagaId);
                    mapper.ConfigureMapping<ChildUpdateSagaMessage>(m => m.MessageId).ToSaga(s => s.SagaId);
                }

                public async Task Handle(StartSagaMessage message, IMessageHandlerContext context)
                {
                    Interlocked.Increment(ref TestContext.HandledStartMessage);

                    await context.SendLocal(new ChildUpdateSagaMessage
                    {
                        MessageId = message.MessageId
                    });

                    await RequestTimeout<TimeoutMessage>(context, DateTime.Now.AddSeconds(15));
                }

                public Task Handle(BaseUpdateSagaMessage message, IMessageHandlerContext context)
                {
                    Data.BaseHandlerValue = true;
                    return Task.FromResult(0);
                }

                public Task Handle(ChildUpdateSagaMessage message, IMessageHandlerContext context)
                {
                    Data.ChildHandlerValue = true;
                    return Task.FromResult(0);
                }

                public Task Timeout(TimeoutMessage message, IMessageHandlerContext context)
                {
                    Interlocked.Increment(ref TestContext.HandledTimeoutMessage);
                    TestContext.StoredBaseHandlerValue = Data.BaseHandlerValue;
                    TestContext.StoredChildHandlerValue = Data.ChildHandlerValue;
                    return Task.FromResult(0);
                }
            }

        }

        class StartSagaMessage : ICommand
        {
            public Guid MessageId { get; set; }
        }

        class BaseUpdateSagaMessage : ICommand
        {
            public Guid MessageId { get; set; }
        }

        class ChildUpdateSagaMessage : BaseUpdateSagaMessage
        {
        }

        class TimeoutMessage : ICommand
        {
        }
    }
}