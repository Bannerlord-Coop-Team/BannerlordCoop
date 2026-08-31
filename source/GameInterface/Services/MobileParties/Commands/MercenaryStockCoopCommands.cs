using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.MobileParties.Commands;

public interface IRefreshMercenaryStocksCoopCommand : ICoopCommand
{
}

public sealed class RefreshMercenaryStocksCoopCommand : LegacyCoopCommand, IRefreshMercenaryStocksCoopCommand
{
    public RefreshMercenaryStocksCoopCommand()
        : base(
            "coop.debug.town",
            "refresh_mercenary_stocks",
            "Refreshes mercenary stocks for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townName", "The exact town name; quote values containing spaces."),
            },
            MercenaryStockDebugCommand.RefreshMercenaryStocks)
    {
    }
}

public interface IRequestMercenaryStockCoopCommand : ICoopCommand
{
}

public sealed class RequestMercenaryStockCoopCommand : LegacyCoopCommand, IRequestMercenaryStockCoopCommand
{
    public RequestMercenaryStockCoopCommand()
        : base(
            "coop.debug.town",
            "request_mercenary_stock",
            "Requests mercenary stock for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townName", "The exact town name; quote values containing spaces."),
            },
            MercenaryStockDebugCommand.RequestMercenaryStock)
    {
    }
}
