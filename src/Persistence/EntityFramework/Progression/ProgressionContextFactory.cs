// <copyright file="ProgressionContextFactory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Progression;

using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory for <see cref="ProgressionContext"/>.
/// </summary>
public class ProgressionContextFactory : IDesignTimeDbContextFactory<ProgressionContext>
{
    /// <inheritdoc />
    public ProgressionContext CreateDbContext(string[] args)
    {
        if (!ConnectionConfigurator.IsInitialized)
        {
            ConnectionConfigurator.Initialize(new ConfigFileDatabaseConnectionStringProvider());
        }

        return new ProgressionContext();
    }
}
