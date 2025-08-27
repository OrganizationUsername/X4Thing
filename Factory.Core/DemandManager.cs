namespace Factory.Core;

public class DemandManager
{
    private readonly Dictionary<Resource, int> _globalPull = []; //These also need to include the entity which is pulling
    private readonly Dictionary<Resource, int> _globalIncoming = []; //These also need to include the entity which is pulling
    private readonly Dictionary<int, Dictionary<Resource, int>> _playerPull = [];
    private readonly Dictionary<int, Dictionary<Resource, int>> _playerIncoming = [];
    private readonly Dictionary<ProductionFacility, Dictionary<Resource, int>> _facilityPull = [];
    private readonly Dictionary<ProductionFacility, Dictionary<Resource, int>> _facilityIncoming = [];

    //This should also include all of the transporters that have accepted a task so we can see what is on the way
    public void Refresh(List<ProductionFacility> facilities)
    {
        _globalPull.Clear();
        _globalIncoming.Clear();
        _playerPull.Clear();
        _playerIncoming.Clear();
        _facilityPull.Clear();
        _facilityIncoming.Clear();

        foreach (var facility in facilities)
        {
            var playerId = facility.PlayerId;
            var storage = facility.GetStorage();

            var pulls = facility.GetPullRequests();
            var pullDict = new Dictionary<Resource, int>();
            foreach (var (res, amt) in pulls)
            {
                pullDict[res] = amt;

                _globalPull.TryAdd(res, 0);
                _globalPull[res] += amt;

                _playerPull.TryAdd(playerId, []);
                _playerPull[playerId].TryAdd(res, 0);
                _playerPull[playerId][res] += amt;
            }

            _facilityPull[facility] = pullDict;

            var incomingDict = new Dictionary<Resource, int>();
            foreach (var (res, _) in storage.GetInventory())
            {
                var incoming = storage.GetIncomingAmount(res);
                if (incoming <= 0) { continue; }

                incomingDict[res] = incoming;

                _globalIncoming.TryAdd(res, 0);
                _globalIncoming[res] += incoming;

                _playerIncoming.TryAdd(playerId, []);
                _playerIncoming[playerId].TryAdd(res, 0);
                _playerIncoming[playerId][res] += incoming;
            }

            _facilityIncoming[facility] = incomingDict;
        }
    }

    public int GetGlobalDemand(Resource res) => _globalPull.GetValueOrDefault(res, 0);
    public int GetGlobalIncoming(Resource res) => _globalIncoming.GetValueOrDefault(res, 0);

    public int GetPlayerDemand(int playerId, Resource res) => _playerPull.TryGetValue(playerId, out var dict) ? dict.GetValueOrDefault(res, 0) : 0;

    public int GetPlayerIncoming(int playerId, Resource res) => _playerIncoming.TryGetValue(playerId, out var dict) ? dict.GetValueOrDefault(res, 0) : 0;

    public int GetFacilityDemand(ProductionFacility facility, Resource res) => _facilityPull.TryGetValue(facility, out var dict) ? dict.GetValueOrDefault(res, 0) : 0;

    public int GetFacilityIncoming(ProductionFacility facility, Resource res) => _facilityIncoming.TryGetValue(facility, out var dict) ? dict.GetValueOrDefault(res, 0) : 0;
}