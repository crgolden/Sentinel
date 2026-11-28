@{
    Schema   = 1
    Repo     = 'Sentinel'
    Workflow = '.github/workflows/main_crgolden-sentinel.yml'
    Sonar    = @{ ProjectKey = 'crgolden_Sentinel' }
    Probe    = @{
        Kind        = 'dotnet'
        Projects    = @('Sentinel.Tests.Unit', 'Sentinel.Tests.Integration')
        Environment = @{ 'Sentinel.Tests.Integration' = @{ ASPNETCORE_ENVIRONMENT = 'Development'; RedisDatabase = '1' } }
        Wrap        = @{ 'Sentinel.Tests.Integration' = @('pwsh', '-NoProfile', '-File', 'scripts/gate/Use-IntegrationRedis.ps1') }
    }
    Steps    = @(
        @{ Name = 'Regenerate lock files and push'; Delivery = $true }
        @{ Name = 'Install SonarQube Cloud scanner'; Tool = 'dotnet-sonarscanner'; Inputs = @('*') }
        @{ Name = 'Install dotnet-coverage'; Tool = 'dotnet-coverage'; Inputs = @('*') }
        @{
            Name   = 'Begin Sonar analysis'
            Sonar  = 'begin'
            Needs  = @('Install SonarQube Cloud scanner')
            Inputs = @('*')
            Run    = @(
                'dotnet-sonarscanner', 'begin', '/k:{sonarProject}', '/o:crgolden', '/d:sonar.host.url=https://sonarcloud.io',
                '/d:sonar.cs.opencover.reportsPaths=coverage.opencover.xml', '/d:sonar.cs.vscoveragexml.reportsPaths=coverage-integration.xml',
                '/d:sonar.exclusions=**/bin/**,**/obj/**', '/d:sonar.coverage.exclusions=**/Program.cs,**/gate.ps1,**/gate.steps.psd1,scripts/gate/**',
                '/d:sonar.qualitygate.wait=true', '/d:sonar.scanner.skipJreProvisioning=true',
                '/d:sonar.scm.use.blame.algorithm=GIT_NATIVE_BLAME', '/d:sonar.branch.name={sonarBranch}'
            )
        }
        @{
            Name   = 'Build with dotnet'
            Sonar  = 'build'
            Inputs = @('*')
            Run    = @('dotnet', 'build', '--no-incremental', '--configuration', 'Release', '/p:RestoreLockedMode=true', '-warnaserror')
        }
        @{ Name = 'Restore local tools'; Run = @('dotnet', 'tool', 'restore'); Inputs = @('dotnet-tools.json'); Always = $true }
        @{
            Name   = 'ReSharper inspection (jb inspectcode)'
            Needs  = @('Build with dotnet', 'Restore local tools')
            Inputs = @('*')
            Remove = @('{output}\sentinel-inspect.sarif')
            Run    = @('dotnet', 'jb', 'inspectcode', 'Sentinel.slnx', '--no-build', '-e=WARNING', '--caches-home={inspectCache}', '--output={output}\sentinel-inspect.sarif')
            Check  = @{ Sarif = '{output}\sentinel-inspect.sarif' }
        }
        @{
            Name   = 'Run unit tests with coverage'
            Needs  = @('Build with dotnet', 'Restore local tools')
            Inputs = @('*', '!Sentinel.Tests.Integration/*')
            Remove = @('Sentinel.Tests.Unit\bin\Release\net11.0\TestResults\unit-tests.trx')
            Run    = @(
                'dotnet', 'coverlet', 'Sentinel.Tests.Unit/bin/Release/net11.0', '--target', 'dotnet',
                '--targetargs', 'test --project Sentinel.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit --report-xunit-trx --report-xunit-trx-filename unit-tests.trx --results-directory=Sentinel.Tests.Unit/bin/Release/net11.0/TestResults',
                '--format', 'opencover', '--output', 'coverage.opencover.xml', '--skipautoprops',
                '--exclude-by-attribute', 'GeneratedCodeAttribute', '--exclude-by-file', '**/obj/**', '--exclude-by-file', '**/Program.cs',
                '--does-not-return-attribute', 'DoesNotReturnAttribute', '--include', '[Sentinel]*'
            )
            Check  = @{ Trx = 'Sentinel.Tests.Unit\bin\Release\net11.0\TestResults\unit-tests.trx'; Floor = 202 }
        }
        @{
            Name        = 'Run integration tests with coverage'
            Needs       = @('Build with dotnet', 'Install dotnet-coverage')
            Inputs      = @('*', '!Sentinel.Tests.Unit/*')
            Environment = @{ ASPNETCORE_ENVIRONMENT = 'Development'; RedisDatabase = '1' }
            Remove      = @('Sentinel.Tests.Integration\bin\Release\net11.0\TestResults\integration-tests.trx')
            Run         = @(
                'pwsh', '-NoProfile', '-File', 'scripts/gate/Use-IntegrationRedis.ps1', 'dotnet-coverage', 'collect',
                'dotnet test --project Sentinel.Tests.Integration --no-build --configuration Release -- --filter-trait Category=Integration --report-xunit-trx --report-xunit-trx-filename integration-tests.trx --results-directory=Sentinel.Tests.Integration/bin/Release/net11.0/TestResults',
                '-f', 'xml', '-o', 'coverage-integration.xml', '-s', 'coverage.settings.xml'
            )
            Check       = @{ Trx = 'Sentinel.Tests.Integration\bin\Release\net11.0\TestResults\integration-tests.trx'; Floor = 20 }
        }
        @{ Name = 'dotnet publish'; Delivery = $true }
        @{
            Name   = 'End Sonar analysis'
            Sonar  = 'end'
            Needs  = @('Begin Sonar analysis', 'Build with dotnet')
            Inputs = @('*')
            Run    = @('dotnet-sonarscanner', 'end')
        }
        @{ Name = 'Fail on open Sonar issues'; Sonar = 'issues'; Needs = @('End Sonar analysis'); Inputs = @('*') }
        @{ Name = 'Run the app on .NET 11'; Delivery = $true }
    )
}
