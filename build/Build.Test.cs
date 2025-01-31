using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using static Nuke.Common.Tools.CloudFoundry.CloudFoundryTasks;
using Newtonsoft.Json.Linq;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.CloudFoundry;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

partial class Build
{
    [Parameter("Name of the installed buildpack that is used for integration tests")]
    readonly string TestBuildpackName = "kerberos-buildpack-test";
    [Parameter("Cloud foundry username")]
    readonly string CfUsername;
    [Parameter("Cloud Foundry Password")]
    readonly string CfPassword;
    [Parameter("Cloud Foundry Endpoint")]
    readonly string CfApiEndpoint;
    [Parameter("Cloud foundry org in which to deploy integration tests")]
    readonly string CfOrg;
    [Parameter("Cloud foundry space in which to deploy integration tests")]
    readonly string CfSpace = "KerberosIntegrationTests";
    [Parameter("KDC server used in integration tests")]
    readonly string IntegrationTestKerbKdc;
    [Parameter("User principal used in integration tests")]
    readonly string IntegrationTestKerbUser;
    [Parameter("Kerberos password used in integration tests")]
    readonly string IntegrationTestKerbPassword;
    [Parameter("SQL server connection string used in integration tests")]
    readonly string IntegrationTestSqlConnectionString;
    [Parameter("User current cf login")]
    readonly bool UseCurrentCfLogin;
    [Parameter("User current cf target (org/space)")]
    readonly bool UseCurrentCfTarget;
    readonly string TestAppName = "KerberosDemo";

    Target CfLogin => _ => _
        .Requires(() => CfUsername, () => CfPassword, () => CfApiEndpoint)
        .OnlyWhenStatic(() => !UseCurrentCfLogin)
        .Unlisted()
        .Executes(() =>
        {
            CloudFoundryApi(c => c.SetUrl(CfApiEndpoint));
            CloudFoundryAuth(c => c
                .SetUsername(CfUsername)
                .SetPassword(CfPassword));
        });
    Target SetCfTargetSpace => _ => _
        .OnlyWhenStatic(() => !UseCurrentCfTarget)
        .After(CfLogin)
        .Executes(() =>
        {
            CloudFoundryTarget(c => c
                .SetOrg(CfOrg));
            CloudFoundryCreateSpace(c => c
                .SetSpace(CfSpace));
            CloudFoundryTarget(c => c
                .SetOrg(CfOrg)
                .SetSpace(CfSpace));
        });

    Target EnsureCfTarget => _ => _
        .DependsOn(CfLogin, SetCfTargetSpace);
    Target InstallBuildpack => _ => _
        .DependsOn(Publish, EnsureCfTarget)
        .Executes(() =>
        {
            var isExisting = CloudFoundryCurl(o => o
                    .SetProcessLogOutput(false)
                    .SetPath("/v3/buildpacks"))
                .StdToJson()
                .SelectTokens("$..name")
                .Select(x => x.Value<string>())
                .Any(buildpackName => buildpackName == TestBuildpackName);
            if (isExisting)
            {
                CloudFoundryUpdateBuildpack(c => c
                    .SetBuildpackName(TestBuildpackName)
                    .SetPath(ArtifactsDirectory / PackageZipName));
            }
            else
            {
                CloudFoundryCreateBuildpack(c => c
                    .SetBuildpackName(TestBuildpackName)
                    .SetPath(ArtifactsDirectory / PackageZipName)
                    .SetPosition(1000));
            }
        });

}
