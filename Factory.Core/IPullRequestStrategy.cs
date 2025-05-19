namespace Factory.Core;

public interface IPullRequestStrategy
{
    IEnumerable<(Resource resource, int amount)> GetRequests(ProductionFacility facility);
}

public class DefaultPullRequestStrategy : IPullRequestStrategy
{
    public IEnumerable<(Resource resource, int amount)> GetRequests(ProductionFacility facility)
    {
        foreach (var (recipe, _) in facility.GetWorkshops())
        {
            foreach (var (resource, perJob) in recipe.Inputs)
            {
                var current = facility.GetStorage().GetTotalIncludingIncoming(resource);
                if (current < perJob) { yield return (resource, perJob - current); }
            }
        }
    }
}

public class SustainedProductionStrategy(int ticks) : IPullRequestStrategy
{
    public IEnumerable<(Resource resource, int amount)> GetRequests(ProductionFacility facility)
    {
        foreach (var (recipe, count) in facility.GetWorkshops())
        {
            var jobsNeeded = ticks * count / recipe.Duration;

            foreach (var (resource, perJob) in recipe.Inputs)
            {
                var required = jobsNeeded * perJob;
                var current = facility.GetStorage().GetAmount(resource);
                var delta = required - current;
                if (delta > 0) { yield return (resource, delta); }
            }
        }
    }
}