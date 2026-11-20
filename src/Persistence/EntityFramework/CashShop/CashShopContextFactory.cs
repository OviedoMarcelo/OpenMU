// <copyright file="CashShopContextFactory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.CashShop;

using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Creates the <see cref="CashShopContext"/> for the design time tools, e.g. to add migrations.
/// </summary>
public class CashShopContextFactory : IDesignTimeDbContextFactory<CashShopContext>
{
    /// <inheritdoc />
    public CashShopContext CreateDbContext(string[] args)
    {
        if (!ConnectionConfigurator.IsInitialized)
        {
            ConnectionConfigurator.Initialize(new ConfigFileDatabaseConnectionStringProvider());
        }

        return new CashShopContext();
    }
}
