using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Options;
using Strive.Core.Interfaces;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Requests;

namespace Strive.Core.Services.Payments.UseCases
{
    public static class OrganizerRules
    {
        public const int MinNameLength = 2;
        public const int MaxNameLength = 80;
        public const int MinAboutLength = 10;
        public const int MaxAboutLength = 600;
        public const int MaxNoteLength = 300;
    }

    public class RequestOrganizerUseCase : IRequestHandler<RequestOrganizerRequest, SuccessOrError<CreatorProfile>>
    {
        private readonly ICreatorRepo _creators;
        private readonly TimeProvider _timeProvider;

        public RequestOrganizerUseCase(ICreatorRepo creators, TimeProvider timeProvider)
        {
            _creators = creators;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<CreatorProfile>> Handle(RequestOrganizerRequest request,
            CancellationToken cancellationToken)
        {
            var creator = await _creators.FindByUserId(request.UserId);
            if (creator == null) return PaymentsError.CreatorNotFound;

            var name = request.OrganizationName?.Trim() ?? "";
            if (name.Length is < OrganizerRules.MinNameLength or > OrganizerRules.MaxNameLength)
                return PaymentsError.InvalidOrganizer(
                    $"The name of your league, school or club needs {OrganizerRules.MinNameLength} to {OrganizerRules.MaxNameLength} characters.");

            var about = request.About?.Trim() ?? "";
            if (about.Length is < OrganizerRules.MinAboutLength or > OrganizerRules.MaxAboutLength)
                return PaymentsError.InvalidOrganizer(
                    $"Tell what you want to stream and why you may: {OrganizerRules.MinAboutLength} to {OrganizerRules.MaxAboutLength} characters.");

            // approved already, or waiting: asking again changes nothing
            if (creator.OrganizerStatus is OrganizerStatus.Approved or OrganizerStatus.Requested) return creator;

            var now = _timeProvider.GetUtcNow();
            creator.OrganizerStatus = OrganizerStatus.Requested;
            creator.OrganizationName = name;
            creator.OrganizerAbout = about;
            creator.OrganizerRequestedAt = now;
            creator.OrganizerDecidedAt = null;
            creator.OrganizerDecisionNote = null;
            creator.UpdatedAt = now;
            await _creators.Save(creator);
            return creator;
        }
    }

    public class DecideOrganizerUseCase : IRequestHandler<DecideOrganizerRequest, SuccessOrError<CreatorProfile>>
    {
        private readonly ICreatorRepo _creators;
        private readonly TimeProvider _timeProvider;

        public DecideOrganizerUseCase(ICreatorRepo creators, TimeProvider timeProvider)
        {
            _creators = creators;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<CreatorProfile>> Handle(DecideOrganizerRequest request,
            CancellationToken cancellationToken)
        {
            var creator = await _creators.FindById(request.CreatorId);
            if (creator == null) return PaymentsError.CreatorNotFound;
            if (creator.OrganizerStatus == OrganizerStatus.None)
                return PaymentsError.InvalidOrganizer("This creator did not ask to be an organizer.");

            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            if (note is {Length: > OrganizerRules.MaxNoteLength})
                return PaymentsError.InvalidOrganizer($"The note can have {OrganizerRules.MaxNoteLength} characters at most.");

            var now = _timeProvider.GetUtcNow();
            creator.OrganizerStatus = request.Approve ? OrganizerStatus.Approved : OrganizerStatus.Rejected;
            creator.OrganizerDecidedAt = now;
            creator.OrganizerDecisionNote = note;
            creator.UpdatedAt = now;
            await _creators.Save(creator);
            return creator;
        }
    }

    public class ListBanksUseCase : IRequestHandler<ListBanksRequest, SuccessOrError<IReadOnlyList<ProviderBank>>>
    {
        private readonly IPaymentProvider _provider;
        private readonly PaymentsOptions _options;

        public ListBanksUseCase(IPaymentProvider provider, IOptions<PaymentsOptions> options)
        {
            _provider = provider;
            _options = options.Value;
        }

        public async Task<SuccessOrError<IReadOnlyList<ProviderBank>>> Handle(ListBanksRequest request,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return PaymentsError.NotEnabled;

            try
            {
                var banks = await _provider.ListBanks(_options.Country, cancellationToken);
                return new SuccessOrError<IReadOnlyList<ProviderBank>>(banks.OrderBy(x => x.Name).ToList());
            }
            catch (PaymentProviderException)
            {
                return PaymentsError.ProviderUnavailable;
            }
        }
    }

    public class ResolveBankAccountUseCase : IRequestHandler<ResolveBankAccountRequest, SuccessOrError<ResolvedAccount>>
    {
        private readonly IPaymentProvider _provider;
        private readonly PaymentsOptions _options;

        public ResolveBankAccountUseCase(IPaymentProvider provider, IOptions<PaymentsOptions> options)
        {
            _provider = provider;
            _options = options.Value;
        }

        public async Task<SuccessOrError<ResolvedAccount>> Handle(ResolveBankAccountRequest request,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return PaymentsError.NotEnabled;
            if (!CreatorRules.IsValidBankCode(request.BankCode) || !CreatorRules.IsValidAccountNumber(request.AccountNumber))
                return PaymentsError.InvalidAccount;

            try
            {
                return await _provider.ResolveAccount(request.BankCode, request.AccountNumber, cancellationToken);
            }
            catch (InvalidAccountException)
            {
                return PaymentsError.InvalidAccount;
            }
            catch (PaymentProviderException)
            {
                return PaymentsError.ProviderUnavailable;
            }
        }
    }

    public class SaveCreatorProfileUseCase : IRequestHandler<SaveCreatorProfileRequest, SuccessOrError<CreatorProfile>>
    {
        private readonly IPaymentProvider _provider;
        private readonly ICreatorRepo _repository;
        private readonly PaymentsOptions _options;
        private readonly TimeProvider _timeProvider;

        public SaveCreatorProfileUseCase(IPaymentProvider provider, ICreatorRepo repository,
            IOptions<PaymentsOptions> options, TimeProvider timeProvider)
        {
            _provider = provider;
            _repository = repository;
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<CreatorProfile>> Handle(SaveCreatorProfileRequest request,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return PaymentsError.NotEnabled;

            var name = request.DisplayName?.Trim() ?? "";
            if (name.Length is < 2 or > CreatorRules.MaxNameLength)
                return PaymentsError.InvalidProfile($"The name must have 2 to {CreatorRules.MaxNameLength} characters.");

            var bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
            if (bio is {Length: > CreatorRules.MaxBioLength})
                return PaymentsError.InvalidProfile($"The text about you can have {CreatorRules.MaxBioLength} characters at most.");

            var existing = await _repository.FindByUserId(request.UserId);
            var now = _timeProvider.GetUtcNow();
            var profile = existing ?? new CreatorProfile(Guid.NewGuid().ToString("N"), request.UserId, name, now)
            {
                Country = _options.Country, Currency = _options.Currency,
            };

            profile.DisplayName = name;
            profile.Bio = bio;
            profile.UpdatedAt = now;

            var bankGiven = !string.IsNullOrWhiteSpace(request.BankCode) || !string.IsNullOrWhiteSpace(request.AccountNumber);
            if (bankGiven)
            {
                if (!CreatorRules.IsValidBankCode(request.BankCode) ||
                    !CreatorRules.IsValidAccountNumber(request.AccountNumber))
                    return PaymentsError.InvalidAccount;

                try
                {
                    // never trust the name that the browser saw: look the account up again
                    var account = await _provider.ResolveAccount(request.BankCode!, request.AccountNumber!,
                        cancellationToken);
                    var banks = await _provider.ListBanks(_options.Country, cancellationToken);

                    var subaccount = new SubaccountRequest(name, request.BankCode!, request.AccountNumber!,
                        _options.PlatformCommissionPercent);
                    if (string.IsNullOrEmpty(profile.ProviderSubaccountCode))
                        profile.ProviderSubaccountCode = await _provider.CreateSubaccount(subaccount, cancellationToken);
                    else await _provider.UpdateSubaccount(profile.ProviderSubaccountCode, subaccount, cancellationToken);

                    profile.BankCode = request.BankCode;
                    profile.BankName = banks.FirstOrDefault(x => x.Code == request.BankCode)?.Name;
                    profile.AccountName = account.AccountName;
                    profile.AccountLast4 = request.AccountNumber![^4..];
                }
                catch (InvalidAccountException)
                {
                    return PaymentsError.InvalidAccount;
                }
                catch (PaymentProviderException)
                {
                    return PaymentsError.ProviderUnavailable;
                }
            }
            else if (existing == null)
            {
                // a creator without a bank account could not be paid: the first save needs it
                return PaymentsError.InvalidProfile("Add the bank account that your earnings are paid into.");
            }

            await _repository.Save(profile);
            return profile;
        }
    }

    public static class CreatorRules
    {
        public const int MaxNameLength = 60;
        public const int MaxBioLength = 500;

        private static readonly Regex BankCode = new("^[A-Za-z0-9_-]{2,12}$", RegexOptions.Compiled);

        // Nigerian account numbers (NUBAN) have ten digits
        private static readonly Regex AccountNumber = new("^[0-9]{10}$", RegexOptions.Compiled);

        public static bool IsValidBankCode(string? code) => code != null && BankCode.IsMatch(code);

        public static bool IsValidAccountNumber(string? number) => number != null && AccountNumber.IsMatch(number);
    }
}
