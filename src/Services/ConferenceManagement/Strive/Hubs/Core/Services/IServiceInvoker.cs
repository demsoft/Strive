using System;
using MediatR;
using Strive.Core.Interfaces;

namespace Strive.Hubs.Core.Services
{
    public interface IServiceInvoker
    {
        IServiceRequestBuilder<Unit> Create(IRequest request);
        IServiceRequestBuilder<Unit> Create(Func<IRequest> requestFactory);
        IServiceRequestBuilder<TResponse> Create<TResponse>(IRequest<TResponse> request);

        IServiceRequestBuilder<TResponse> Create<TResponse>(Func<IRequest<TResponse>> request);

        IServiceRequestBuilder<TResponse> Create<TResponse>(IRequest<SuccessOrError<TResponse>> request);

        IServiceRequestBuilder<TResponse> Create<TResponse>(Func<IRequest<SuccessOrError<TResponse>>> requestFactory);
    }
}
