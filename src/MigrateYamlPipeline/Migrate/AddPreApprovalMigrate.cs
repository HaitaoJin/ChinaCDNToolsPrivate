﻿using Microsoft.Cloud.MooncakeService.Common;
using MigrateYamlPipeline.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace MigrateYamlPipeline.Migrate
{
    public class AddPreApprovalMigrate : MigrateBase
    {
        private readonly AddPreApprovalMigrateOptions migrateOptions;
        private readonly DevOpsHttpClient devOpsHttpClient;

        public AddPreApprovalMigrate(AddPreApprovalMigrateOptions migrateOptions)
        {
            Requires.Argument("--org", migrateOptions.Organization).NotNullOrEmpty();
            Requires.Argument("--project", migrateOptions.Project).NotNullOrEmpty();
            Requires.Argument("--pat", migrateOptions.PAT).NotNullOrEmpty();

            this.migrateOptions = migrateOptions;
            this.devOpsHttpClient = new DevOpsHttpClient(migrateOptions.Organization, migrateOptions.Project, migrateOptions.PAT);
        }


        public override async Task Migrate()
        {
            foreach (var stageNode in stages)
            {
                var displayName = ((YamlScalarNode)stageNode["displayName"]).Value;
                var approvals = classicStages.FirstOrDefault(p => p["Name"].ToString() == displayName)["PreDeployApprovals"]["Approvals"].AsArray().Where(p => p["IsAutomated"].ToString() == "false");
                if (approvals.Count() > 0)
                {
                    // Env Add Pre Approval
                    if (((YamlMappingNode)stageNode).Children.ContainsKey(new YamlScalarNode("variables")))
                    {
                        var variables = stageNode["variables"];

                        if (variables.NodeType == YamlNodeType.Sequence)
                        {
                            var variablesNode = (YamlSequenceNode)stageNode["variables"];
                            if (variablesNode.Children.Any(p => p["name"].ToString() == "ob_deploymentjob_environment"))
                            {
                                var environmentJobName = variablesNode.Children.First(p => p["name"].ToString() == "ob_deploymentjob_environment")["value"].ToString();
                                var environment = devOpsHttpClient.GetEnvironmentsAsync(environmentJobName).Result.FirstOrDefault();
                                if (environment != null)
                                {
                                    var approvalChecks = await devOpsHttpClient.GetApprovalCheckAsync(environment);
                                    if (!approvalChecks.Any(p => p.Type.Name == "Approval"))
                                    {
                                        await devOpsHttpClient.AddPreApprovalAsync(environment, approvals.Select(p => p["Approver"]["id"].ToString()).ToList());
                                    }
                                }
                            }
                        }
                        else if(variables.NodeType == YamlNodeType.Mapping)
                        {
                            var variablesNode = (YamlMappingNode)stageNode["variables"];
                            if (variablesNode.Children.ContainsKey(new YamlScalarNode("ob_deploymentjob_environment")))
                            {
                                var environmentJobName = variablesNode[new YamlScalarNode("ob_deploymentjob_environment")].ToString();
                                var environment = devOpsHttpClient.GetEnvironmentsAsync(environmentJobName).Result.FirstOrDefault();
                                if (environment != null)
                                {
                                    var approvalChecks = await devOpsHttpClient.GetApprovalCheckAsync(environment);
                                    if (!approvalChecks.Any(p => p.Type.Name == "Approval"))
                                    {
                                        await devOpsHttpClient.AddPreApprovalAsync(environment, approvals.Select(p => p["Approver"]["id"].ToString()).ToList());
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public class AddPreApprovalMigrateOptions
    {
        public string Organization { get; set; }

        public string Project { get; set; }

        public string PAT { get; set; }
    }
}
