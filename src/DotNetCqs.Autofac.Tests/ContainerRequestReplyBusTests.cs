﻿using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotNetCqs.Autofac.Tests
{
    public class ContainerRequestReplyBusTests
    {
        [Fact]
        public void allow_only_one_query_handler()
        {
            var storage = Substitute.For<ICqsStorage>();
            var container = Substitute.For<IContainer>();
            var scope = Substitute.For<ILifetimeScope>();
            var handler1 = Substitute.For<IRequestHandler<Request<string>, string>>();
            var handler2 = Substitute.For<IRequestHandler<Request<string>, string>>();
            var request = Substitute.For<Request<string>>();
            storage.PopRequestAsync().Returns(Task.FromResult((IRequest)request));
            container.BeginLifetimeScope().Returns(scope);
            var type = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(string));
            var collectionType = typeof(IEnumerable<>).MakeGenericType(type);
            scope.Resolve(collectionType)
                .Returns(new object[] {handler1, handler2});

            var sut = new ContainerRequestReplyBus(container);
            var task = sut.ExecuteAsync(request);
            Action actual = task.Wait;

            actual.ShouldThrow<OnlyOneHandlerAllowedException>();
        }

        [Fact]
        public async Task make_sure_that_a_real_handler_works()
        {
            var storage = Substitute.For<ICqsStorage>();
            var container = Substitute.For<IContainer>();
            var scope = Substitute.For<ILifetimeScope>();
            var handler1 = Substitute.For<IRequestHandler<MyRequest, string>>();
            var request = new MyRequest();
            handler1.ExecuteAsync(request).Returns(Task.FromResult("Hello world"));
            storage.PopRequestAsync().Returns(Task.FromResult((IRequest)request));
            container.BeginLifetimeScope().Returns(scope);
            var collectionType = typeof(IEnumerable<IRequestHandler<MyRequest,string>>);
            scope.Resolve(collectionType)
                .Returns(new object[] { handler1 });

            var sut = new ContainerRequestReplyBus(container);
            var actual = await sut.ExecuteAsync(request);

            actual.Should().Be("Hello world");
        }

        public class MyRequest : Request<string>
        {
             
        }

        [Fact]
        public void a_query_handler_is_mandatory()
        {
            var storage = Substitute.For<ICqsStorage>();
            var container = Substitute.For<IContainer>();
            var scope = Substitute.For<ILifetimeScope>();
            var request = Substitute.For<Request<string>>();
            storage.PopRequestAsync().Returns(Task.FromResult((IRequest)request));
            container.BeginLifetimeScope().Returns(scope);
            scope.Resolve(typeof(IEnumerable<IRequestHandler<Request<string>, string>>))
                .Returns(new object[0]);

            var sut = new ContainerRequestReplyBus(container);
            var task = sut.ExecuteAsync(request);
            Action actual = task.Wait;

            actual.ShouldThrow<CqsHandlerMissingException>();
        }

        [Fact]
        public async Task invoke_the_handler_successfully()
        {
            var storage = Substitute.For<ICqsStorage>();
            var container = Substitute.For<IContainer>();
            var scope = Substitute.For<ILifetimeScope>();
            var handler1 = Substitute.For<IRequestHandler<Request<string>, string>>();
            var request = Substitute.For<Request<string>>();
            handler1.ExecuteAsync(request).Returns(Task.FromResult("Hello world"));
            storage.PopRequestAsync().Returns(Task.FromResult((IRequest)request));
            container.BeginLifetimeScope().Returns(scope);
            var type = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(string));
            scope.Resolve(typeof(IEnumerable<>).MakeGenericType(type))
                .Returns(new object[] { handler1 });

            var sut = new ContainerRequestReplyBus(container);
            var actual = await sut.ExecuteAsync(request);

            actual.Should().Be("Hello world");
        }

        [Fact]
        public async Task do_not_catch_handler_Exceptions()
        {
            var container = Substitute.For<IContainer>();
            var scope = Substitute.For<ILifetimeScope>();
            var handler1 = Substitute.For<IRequestHandler<Request<string>, string>>();
            var request = Substitute.For<Request<string>>();
            handler1
                .When(x => x.ExecuteAsync(request))
                .Do(x => { throw new InvalidCastException(); });
            container.BeginLifetimeScope().Returns(scope);
            var type = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(string));
            scope.Resolve(typeof(IEnumerable<>).MakeGenericType(type))
                .Returns(new object[] { handler1 });

            var sut = new ContainerRequestReplyBus(container);
            try
            {
                sut.ExecuteAsync(request).Wait();
                Assert.False(true, "Query did not fail");
            }
            catch (AggregateException exception)
            {
                scope.Received().Resolve(typeof(IEnumerable<>).MakeGenericType(type));
                exception.InnerException.Should().BeOfType<InvalidCastException>();
            }



        }
    }
}
