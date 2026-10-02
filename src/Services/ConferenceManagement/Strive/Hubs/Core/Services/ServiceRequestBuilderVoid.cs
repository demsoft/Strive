using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Interfaces;

namespace Strive.Hubs.Core.Services
{
    /// <summary>
    ///     Request builder for requests without a response (<see cref="IRequest" />). The client still receives a
    ///     <see cref="SuccessOrError{T}" /> of <see cref="Unit" />.
    /// </summary>
    public class ServiceRequestBuilderVoid : ServiceRequestBuilderBase<Unit>
    {
        private readonly IMediator _mediator;
        private readonly Lazy<IRequest> _lazyRequest;

        public ServiceRequestBuilderVoid(Func<IRequest> requestFactory, IMediator mediator,
            ServiceInvokerContext context) : base(context)
        {
            _lazyRequest = new Lazy<IRequest>(requestFactory);
            _mediator = mediator;
        }

        protected override async Task<SuccessOrError<Unit>> CreateRequest(CancellationToken token)
        {
            await _mediator.Send(_lazyRequest.Value, token);
            return SuccessOrError<Unit>.Succeeded(Unit.Value);
        }

        protected override Type GetRequestType()
        {
            return _lazyRequest.Value.GetType();
        }
    }
}
