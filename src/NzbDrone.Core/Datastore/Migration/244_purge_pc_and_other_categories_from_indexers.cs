using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using FluentMigrator;
using Newtonsoft.Json.Linq;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(244)]
    public class purge_pc_and_other_categories_from_indexers : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Execute.WithConnection(PurgePcAndOtherCategories);
        }

        private void PurgePcAndOtherCategories(IDbConnection conn, IDbTransaction tran)
        {
            var updatedIndexers = new List<object>();

            using (var selectCommand = conn.CreateCommand())
            {
                selectCommand.Transaction = tran;
                selectCommand.CommandText = "SELECT \"Id\", \"Settings\" FROM \"Indexers\" WHERE \"ConfigContract\" IN ('NewznabSettings', 'TorznabSettings')";

                using (var reader = selectCommand.ExecuteReader())
                {
                    var indexerSettings = new List<(int Id, string Settings)>();

                    while (reader.Read())
                    {
                        var idIndex = reader.GetOrdinal("Id");
                        var settingsIndex = reader.GetOrdinal("Settings");

                        indexerSettings.Add((reader.GetInt32(idIndex), reader.GetString(settingsIndex)));
                    }

                    foreach (var (id, settingsJson) in indexerSettings)
                    {
                        if (string.IsNullOrWhiteSpace(settingsJson))
                        {
                            continue;
                        }

                        var settings = Json.Deserialize<Dictionary<string, object>>(settingsJson);

                        string categoryKey = null;
                        if (settings.ContainsKey("categories"))
                        {
                            categoryKey = "categories";
                        }
                        else if (settings.ContainsKey("Categories"))
                        {
                            categoryKey = "Categories";
                        }

                        if (categoryKey != null && settings[categoryKey] is JArray categoriesArray)
                        {
                            var originalCount = categoriesArray.Count;
                            var filteredList = new List<int>();

                            foreach (var token in categoriesArray)
                            {
                                if (int.TryParse(token.ToString(), out var catId) && catId >= 1000 && catId < 2000)
                                {
                                    filteredList.Add(catId);
                                }
                            }

                            if (filteredList.Count != originalCount)
                            {
                                settings[categoryKey] = filteredList;
                                updatedIndexers.Add(new
                                {
                                    Settings = settings.ToJson(),
                                    Id = id
                                });
                            }
                        }
                    }
                }
            }

            if (updatedIndexers.Any())
            {
                var updateSql = "UPDATE \"Indexers\" SET \"Settings\" = @Settings WHERE \"Id\" = @Id";
                conn.Execute(updateSql, updatedIndexers, transaction: tran);
            }
        }
    }
}
