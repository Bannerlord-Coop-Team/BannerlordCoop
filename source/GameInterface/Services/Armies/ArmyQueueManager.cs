using Common.Logging;
using GameInterface;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

public class ArmyQueueManager
{
    private static ILogger Logger = LogManager.GetLogger<ArmyQueueManager>();
    private List<Tuple<string, List<string>>> queue = new List<Tuple<string, List<string>>>();

    private TimeSpan delay = TimeSpan.FromSeconds(1); // Adjust delay as needed
    private System.Timers.Timer timer;

    public ArmyQueueManager()
    {
        timer = new System.Timers.Timer();
        timer.Interval = delay.TotalMilliseconds;
        timer.Elapsed += TimerElapsed;
        timer.AutoReset = true;
        timer.Start();
    }

    public void Enqueue(String armyId, List<String> mobilePartyIds)
    {
        queue.Add(new Tuple<string, List<string>>(armyId, mobilePartyIds));

        if (!timer.Enabled)
        {
            timer.Start();
            Console.WriteLine("Timer started.");
        }
    }

    private void TimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {

        List<String> armiesToRemove = new List<String>();
        
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            Logger.Error("Unable to resolve {objectManager}", typeof(IObjectManager));
            return;
        }

        foreach (var value in queue)
        {
            if (value == null)
            {
                continue;
            }
            String armyId = value.Item1;
            List<String> mobilePartiesId = value.Item2;
            
            if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
            {
                Logger.Error("Unable to find Army ({armyId})", armyId);
                continue;
            }
            List<MobileParty> newListToUpdate = new List<MobileParty>();
            foreach (var mobilePartyId in mobilePartiesId)
            {
                if (objectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty) == false)
                {
                    Logger.Error("Unable to find MobileParty ({mobilePartyId})", mobilePartyId);
                    return;
                }
                newListToUpdate.Add(mobileParty);
            }
            ArmyPatches.SetMobilePartyListInArmy(newListToUpdate, army);
            armiesToRemove.Add(armyId);
        }

        // Remove processed armies from the queue
        foreach (String armyIdToRemove in armiesToRemove)
        {
            // Remove all the armyId in the tuple list equal to the armyIdToRemove
            queue.RemoveAll(x => x.Item1 == armyIdToRemove);
        }

        if(queue.Count == 0)
        {
            timer.Stop();
            Console.WriteLine("Queue is empty. Stopping timer.");
            return;
        }
    }
}
