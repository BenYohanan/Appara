using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Constants
{
	/// <summary>
	/// Schemes for endpoints both surfaces hit: the web dashboard over the
	/// Identity cookie, the mobile app over a JWT.
	///
	/// Spelled out as literals because IdentityConstants.ApplicationScheme and
	/// JwtBearerDefaults.AuthenticationScheme are static readonly, not const,
	/// so they cannot be used in an attribute argument. These must stay in step
	/// with the framework's values.
	/// </summary>
	public static class AuthSchemes
	{
		/// <summary>IdentityConstants.ApplicationScheme</summary>
		public const string Cookie = "Identity.Application";

		/// <summary>JwtBearerDefaults.AuthenticationScheme</summary>
		public const string Bearer = "Bearer";

		public const string CookieOrBearer = Cookie + "," + Bearer;
	}

	public static class SafeHavenConstants
	{
		/// <summary>
		/// The institution code every Apparcus sub-account sits under. Account
		/// numbers are only unique within a bank, so anything deciding whether a
		/// destination is one of ours has to check this too — matching on the
		/// number alone could send money to a stranger who happens to share it
		/// at another bank.
		/// </summary>
		public const string BankCode = "090286";
	}

	public static class WithdrawalFeeConstants
	{
		/// <summary>
		/// Withdrawals we cover for a project before the fee starts biting.
		/// Money contributed jointly is nearly always taken out in bulk, so two
		/// covers the ordinary case without us absorbing the cost of a project
		/// that withdraws piecemeal.
		/// </summary>
		public const int FreeWithdrawalsPerProject = 2;

		/// <summary>
		/// What a withdrawal costs once the free ones are spent: N10 to Safe
		/// Haven MFB, N0.075 VAT and N4.98875 to Apparcus, charged as a flat
		/// N15 and payable either in Beads or out of the project balance.
		/// </summary>
		public const decimal WithdrawalFeeBeads = 15m;

		public const decimal RealTransferFee = 10.75m;
	}

	/// <summary>
	/// The one-time fee for opening a money project. Charged once per project,
	/// from the user's Beads first, then their Personal Account. With neither
	/// able to cover it the project is still created and the fee is owed: it is
	/// taken from the project's own balance once that reaches
	/// <see cref="CollectFromProjectAt"/>, and the project cannot be withdrawn
	/// from until it has been paid.
	/// </summary>
	public static class ProjectFeeConstants
	{
		/// <summary>Projects a user opens before the fee applies.</summary>
		public const int FreeProjects = 5;

		public const decimal FeeNaira = 50m;

		/// <summary>
		/// The project balance at which an owed fee is taken from the project
		/// itself — enough that the fee is a small slice of what was raised.
		/// </summary>
		public const decimal CollectFromProjectAt = 1_000m;
	}

	/// <summary>
	/// One band of the published transaction charge. A contribution is priced
	/// by the band its amount falls in: the bank takes <see cref="BankFlat"/>
	/// or <see cref="BankPercentage"/> of the amount, Apparcus takes
	/// <see cref="ApparcusFlat"/>, and the contributor pays the two together on
	/// top of what they are contributing.
	/// </summary>
	public sealed class TransactionChargeBand
	{
		public required decimal MinAmount { get; init; }

		/// <summary>Null on the top band, which has no upper bound.</summary>
		public decimal? MaxAmount { get; init; }

		public decimal BankFlat { get; init; }

		/// <summary>A fraction, not a percentage: 0.005m is 0.5%.</summary>
		public decimal BankPercentage { get; init; }

		public decimal ApparcusFlat { get; init; }

		/// <summary>
		/// Beads the project owner earns per contribution in this band while the
		/// project is still inside its early window.
		/// </summary>
		public decimal EarlyBeads { get; init; }

		public decimal StandardBeads { get; init; }

		public bool Covers(decimal amount) =>
			amount >= MinAmount && (MaxAmount == null || amount <= MaxAmount);

		public decimal BankShareOn(decimal amount) =>
			BankFlat + decimal.Round(amount * BankPercentage, 2, MidpointRounding.AwayFromZero);

		/// <summary>The whole charge the contributor pays on top of the amount.</summary>
		public decimal ChargeOn(decimal amount) => BankShareOn(amount) + ApparcusFlat;

		public decimal BeadsFor(bool early) => early ? EarlyBeads : StandardBeads;

		/// <summary>
		/// The band as it reads on the public pricing page. Bands start a kobo
		/// above the one below so nothing falls between them, but the published
		/// figure is the first whole naira — "1,001 to 10,000", not "1,000".
		/// </summary>
		public string AmountLabel
		{
			get
			{
				var from = decimal.Ceiling(MinAmount);

				return MaxAmount == null
					? $"{from:N0} and above"
					: $"{from:N0} to {MaxAmount:N0}";
			}
		}

		public string ChargeLabel => BankPercentage > 0
			? $"{BankPercentage * 100:0.##}% + {ApparcusFlat + BankFlat:N0}"
			: $"{BankFlat + ApparcusFlat:N0}";

		public string BeadsLabel
		{
			get
			{
				if (EarlyBeads == StandardBeads)
					return $"{StandardBeads:N0} all through";

				return $"{EarlyBeads:N0} in the first "
					+ $"{TransactionChargeConstants.EarlyWindowHours / 24} days, "
					+ $"then {StandardBeads:N0}";
			}
		}
	}

	/// <summary>
	/// The published transaction charge, and the single place it is decided.
	/// The contributor's browser shows a preview of this, but nothing it sends
	/// is trusted: the server prices every contribution itself before a virtual
	/// account is opened for it.
	/// </summary>
	public static class TransactionChargeConstants
	{
		/// <summary>
		/// How long after a project is created its contributions still earn the
		/// higher Beads reward. Five days.
		/// </summary>
		public const int EarlyWindowHours = 120;

		public static readonly IReadOnlyList<TransactionChargeBand> Bands =
		[
			new TransactionChargeBand
			{
				MinAmount = 0m,
				MaxAmount = 1_000m,
				BankFlat = 5m,
				ApparcusFlat = 4m,
				EarlyBeads = 0m,
				StandardBeads = 0m,
			},
			new TransactionChargeBand
			{
				MinAmount = 1_000.01m,
				MaxAmount = 10_000m,
				BankPercentage = 0.005m,
				ApparcusFlat = 5m,
				EarlyBeads = 2m,
				StandardBeads = 2m,
			},
			new TransactionChargeBand
			{
				MinAmount = 10_000.01m,
				MaxAmount = 100_000m,
				BankFlat = 50m,
				ApparcusFlat = 9m,
				EarlyBeads = 3m,
				StandardBeads = 2m,
			},
			new TransactionChargeBand
			{
				MinAmount = 100_000.01m,
				MaxAmount = null,
				BankFlat = 50m,
				ApparcusFlat = 49m,
				EarlyBeads = 10m,
				StandardBeads = 5m,
			},
		];

		/// <summary>
		/// The band an amount is priced in. Falls back to the top band so an
		/// amount can never slip through unpriced.
		/// </summary>
		public static TransactionChargeBand BandFor(decimal amount) =>
			Bands.FirstOrDefault(b => b.Covers(amount)) ?? Bands[^1];

		public static decimal ChargeOn(decimal amount) => BandFor(amount).ChargeOn(amount);

		public static decimal BeadsFor(decimal amount, bool early) =>
			BandFor(amount).BeadsFor(early);

		public static decimal BeadsFor(decimal amount, double hoursSinceProjectCreated) =>
			BeadsFor(amount, hoursSinceProjectCreated <= EarlyWindowHours);

		/// <summary>
		/// A contribution's own kobo, between 0.10 and 0.99. Two people paying
		/// the same amount into the same project would otherwise open virtual
		/// accounts expecting an identical transfer, and a credit could be
		/// matched to the wrong one.
		/// </summary>
		public static decimal NewKoboSuffix() =>
			Random.Shared.Next(10, 100) / 100m;

		/// <summary>
		/// What the contributor is asked to transfer: the amount, the charge for
		/// its band, and the kobo that keeps it distinguishable.
		/// </summary>
		public static decimal PayableFor(decimal amount) =>
			amount + ChargeOn(amount) + NewKoboSuffix();
	}

	/// <summary>One buyable block of Type-B records.</summary>
	public sealed class RecordPack
	{
		public required int Size { get; init; }
		public required decimal PriceNaira { get; init; }
	}

	/// <summary>
	/// Type-B projects keep records rather than take money, so they are paid
	/// for by the record. A pack covers the user's Type-B projects together,
	/// not one project each.
	/// </summary>
	public static class RecordQuotaConstants
	{
		/// <summary>Smallest first — the order they are offered in.</summary>
		public static readonly IReadOnlyList<RecordPack> Packs =
		[
			new RecordPack { Size = 500, PriceNaira = 299m },
			new RecordPack { Size = 700, PriceNaira = 429m },
			new RecordPack { Size = 1_500, PriceNaira = 729m },
		];

		/// <summary>What someone gets if they buy without choosing a pack.</summary>
		public static RecordPack DefaultPack => Packs[0];

		/// <summary>
		/// The pack of a given size, or null. Sizes are the only thing a client
		/// sends, so an unknown one has to be refused rather than guessed at.
		/// </summary>
		public static RecordPack? PackOfSize(int size) =>
			Packs.FirstOrDefault(p => p.Size == size);

		public const int PackSize = 500;
		public const decimal PackPriceNaira = 299m;
	}
}