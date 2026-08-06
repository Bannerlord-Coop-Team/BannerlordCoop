using Common.Messaging;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Locations;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace GameInterface.Services.Time.UI;

public class MissionMapTimeView : MissionView, ILocationMissionBehavior
{
    private const int LayerOrder = 10;

    private readonly IMessageBroker messageBroker;
    private readonly ITimeControlInterface timeControlInterface;

    private MissionMapTimeVM dataSource;
    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private bool subscribed;

    public MissionMapTimeView(IMessageBroker messageBroker, ITimeControlInterface timeControlInterface)
    {
        this.messageBroker = messageBroker;
        this.timeControlInterface = timeControlInterface;
    }

    public override void OnMissionScreenInitialize()
    {
        base.OnMissionScreenInitialize();
        
        dataSource = new MissionMapTimeVM(timeControlInterface.GetTimeControl());
        gauntletLayer = new GauntletLayer("MissionMapTime", LayerOrder);
        movie = gauntletLayer.LoadMovie("MissionMapTime", dataSource);
        
        MissionScreen.AddLayer(gauntletLayer);
        
        messageBroker.Subscribe<NetworkChangeTimeControlMode>(HandleTimeControlModeChanged);
        
        subscribed = true;
    }

    public override void OnMissionScreenFinalize()
    {
        if (subscribed)
        {
            messageBroker.Unsubscribe<NetworkChangeTimeControlMode>(HandleTimeControlModeChanged);
            subscribed = false;
        }

        if (gauntletLayer != null)
        {
            if (movie != null)
            {
                gauntletLayer.ReleaseMovie(movie);
            }
            MissionScreen.RemoveLayer(gauntletLayer);
        }
        
        dataSource?.OnFinalize();
        movie = null;
        gauntletLayer = null;
        dataSource = null;
        
        base.OnMissionScreenFinalize();
    }

    private void HandleTimeControlModeChanged(MessagePayload<NetworkChangeTimeControlMode> payload)
    {
        dataSource?.SetTimeControlMode(payload.What.NewControlMode);
    }
}