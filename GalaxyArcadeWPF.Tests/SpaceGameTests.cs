using System.Windows.Input;
using GalaxyArcadeWPF;
using Xunit;

namespace GalaxyArcadeWPF.Tests;

public sealed class SpaceGameTests
{
    [Fact]
    public void NewGameWaitsForLaunchBeforeMovingOrFiring()
    {
        SpaceGame game = new();
        double initialX = game.PlayerX;

        game.Update(0.05, new HashSet<Key> { Key.Right, Key.Space });

        Assert.Equal(ArcadeStatus.Ready, game.Status);
        Assert.Equal(initialX, game.PlayerX);
        Assert.Empty(game.Projectiles);
    }

    [Fact]
    public void LaunchedGameMovesAndFires()
    {
        SpaceGame game = new();
        game.Start();
        double initialX = game.PlayerX;

        game.Update(0.05, new HashSet<Key> { Key.D, Key.Space });

        Assert.Equal(ArcadeStatus.Playing, game.Status);
        Assert.True(game.PlayerX > initialX);
        Assert.Single(game.Projectiles);
    }

    [Fact]
    public void PausedGameFreezesPlayerUntilResumed()
    {
        SpaceGame game = new();
        game.Start();
        game.TogglePause();
        double pausedX = game.PlayerX;

        game.Update(0.05, new HashSet<Key> { Key.A });

        Assert.Equal(ArcadeStatus.Paused, game.Status);
        Assert.Equal(pausedX, game.PlayerX);

        game.TogglePause();
        game.Update(0.05, new HashSet<Key> { Key.A });

        Assert.Equal(ArcadeStatus.Playing, game.Status);
        Assert.True(game.PlayerX < pausedX);
    }

    [Fact]
    public void PlayerProjectileDestroysEnemyAndAwardsPoints()
    {
        SpaceGame game = new();
        game.Start();
        game.Enemies.Add(new EnemyShip(450, 200, 0, 20, 90));
        game.Projectiles.Add(new Projectile(450, 200, 0, 0, false));

        game.Update(0.01, new HashSet<Key>());

        Assert.Equal(25, game.Score);
        Assert.Empty(game.Enemies);
        Assert.Empty(game.Projectiles);
    }

    [Fact]
    public void EnemyCollisionConsumesOneShieldAndTemporaryInvulnerabilityPreventsRepeatDamage()
    {
        SpaceGame game = new();
        game.Start();
        game.Enemies.Add(new EnemyShip(game.PlayerX, game.PlayerY, 0, 20, 90));

        game.Update(0.01, new HashSet<Key>());

        Assert.Equal(2, game.Lives);
        Assert.True(game.InvulnerabilitySeconds > 0);

        game.Enemies.Add(new EnemyShip(game.PlayerX, game.PlayerY, 0, 20, 90));
        game.Update(0.01, new HashSet<Key>());

        Assert.Equal(2, game.Lives);
    }

    [Fact]
    public void ResetClearsRunAndReturnsToReadyState()
    {
        SpaceGame game = new();
        game.Start();
        game.ScoreEnemyForTest();

        game.Reset();

        Assert.Equal(ArcadeStatus.Ready, game.Status);
        Assert.Equal(0, game.Score);
        Assert.Equal(3, game.Lives);
        Assert.Empty(game.Enemies);
        Assert.Empty(game.Projectiles);
    }
}

internal static class SpaceGameTestExtensions
{
    public static void ScoreEnemyForTest(this SpaceGame game)
    {
        game.Enemies.Add(new EnemyShip(450, 200, 0, 20, 90));
        game.Projectiles.Add(new Projectile(450, 200, 0, 0, false));
        game.Update(0.01, new HashSet<Key>());
    }
}