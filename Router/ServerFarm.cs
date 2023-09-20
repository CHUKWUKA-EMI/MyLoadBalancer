using System;
using System.Threading.Channels;
using Newtonsoft.Json;
using Postgrest.Attributes;
using Postgrest.Models;
using Supabase;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace MyLoadBalancer.Router
{
    [Table("serverFarm")]
    public class ServerFarmTable : BaseModel
    {
        [PrimaryKey("id",false)]
        public long Id { get; set; }
        [Column("url")]
        public Uri Url { get; set; }
        [Column("healthCheckPath")]
        public string HealthCheckPath { get; set; }
        [Column("weight")]
        public int Weight { get; set; }
        [Column("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    public class ServerFarm
    {
        private readonly Client _client;
        private readonly Queue<Target> _roundRobinCache = new Queue<Target>();

      public ServerFarm(Client client)
        {
            _client = client;
            this.GetTargets().GetAwaiter().GetResult();
            this.SubScribeToConfigUpdates();
        }


        public async Task<List<ServerFarmTable>> GetTargets()
        {
            var result = await _client.From<ServerFarmTable>().Get();
            // clear cache
            _roundRobinCache.Clear();
            foreach (var model in result.Models)
            {
                var target = new Target
                {
                    Id = model.Id,
                    HealthCheckPath = model.HealthCheckPath,
                    Url = model.Url,
                    Weight = model.Weight != 0 ? model.Weight : Constants.DefaultWeight,
                    RequestsCount = Constants.DefaulRequestsCount
                };
                if (!_roundRobinCache.Contains(target))
                {
                    _roundRobinCache.Enqueue(target);
                }
            }
           
            return result.Models;
        }

        public async void SubScribeToConfigUpdates()
        {
           
            await _client.Realtime.ConnectAsync();
            // Listen for Database Inserts
            await _client.From<ServerFarmTable>()
                   .On(ListenType.Inserts, (sender, change) =>
                   {
                       var model = change.Model<ServerFarmTable>();
                       if(model != null)
                       {
                           var target = new Target
                           {
                               Id = model.Id,
                               HealthCheckPath = model.HealthCheckPath,
                               Url = model.Url,
                               Weight = model.Weight !=0?model.Weight:Constants.DefaultWeight,
                               RequestsCount = Constants.DefaulRequestsCount
                           };
                           _roundRobinCache.Enqueue(target);
                       }
                   });
            // Listen for Database Updates
            await _client.From<ServerFarmTable>()
                   .On(ListenType.Updates, (sender, change) =>
                   {
                       var model = change.Model<ServerFarmTable>();
                       var oldModel = change.OldModel<ServerFarmTable>();
                       if(oldModel != null && model !=null)
                       {
                           foreach (var target in _roundRobinCache)
                           {
                               if (target.Id ==oldModel.Id)
                               {
                                   target.Id = model.Id;
                                   target.HealthCheckPath = model.HealthCheckPath;
                                   target.Url = model.Url;
                                   target.Weight = model.Weight != 0 ? model.Weight : Constants.DefaultWeight;
                               }
                           }
                       }
                   });

            // Listen for Database Deletes
            await _client.From<ServerFarmTable>()
                   .On(ListenType.Deletes, (sender, change) =>
                   {
                       var deletedModel = change.OldModel<ServerFarmTable>();
                       
                       if (deletedModel != null)
                       {
                           var tempQueue = new Queue<Target>();
                           // filter out the elements that have not been deleted and store them in
                           // a temporary queue
                           foreach (var target in _roundRobinCache)
                           {
                               if (target.Id != deletedModel.Id)
                               {
                                   tempQueue.Enqueue(target);
                               }
                           }
                           // empty the main queue
                           while (_roundRobinCache.Count > 0)
                           {
                               _roundRobinCache.Dequeue();
                           }
                           // push the non-deleted elements back to the main queue and empty the temporary queue
                           while(tempQueue.Count != 0)
                           {
                               _roundRobinCache.Enqueue(tempQueue.Peek());
                               tempQueue.Dequeue();
                           }
                       }
                       
                   });
        }

        public string RoundRobinTargets()
        {
            var targetUrlString = "";
            var currentTarget = _roundRobinCache.Peek();
            if (currentTarget.MustGoToNext())
            {
                currentTarget = _roundRobinCache.Dequeue();
                currentTarget.RequestsCount = Constants.DefaulRequestsCount;
                _roundRobinCache.Enqueue(currentTarget);
                return RoundRobinTargets();
            }
            else
            {
                targetUrlString = currentTarget.Url.ToString();
                if(targetUrlString.EndsWith("/"))
                {
                    targetUrlString = targetUrlString.Substring(0, targetUrlString.LastIndexOf("/"));
                }
            }
            return targetUrlString;
        }
    }
}

