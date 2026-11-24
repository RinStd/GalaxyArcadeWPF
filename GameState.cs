using System.Windows.Input;

namespace GalaxyArcadeWPF;

internal enum ArcadeStatus
{
    Ready,
    Playing,
    Paused,
    GameOver
}

internal sealed class SpaceGame
{
    public const double FieldWidth = 900;
    public const double FieldHeight = 600;

    private readonly Random _random = new();
    private double _shotCooldown;
    private double _spawnCooldown = 0.45;
    private double _animationClock;

    public List<Star> Stars { get; } = new();
    public List<Projectile> Projectiles { get; } = new();
    public List<EnemyShip> Enemies { get; } = new();
    public List<Particle> Particles { get; } = new();
    public ArcadeStatus Status { get; private set; } = ArcadeStatus.Ready;
    public double PlayerX { get; private set; } = FieldWidth / 2;
    public double PlayerY { get; private set; } = FieldHeight - 55;
    public double InvulnerabilitySeconds { get; private set; }
    public int Score { get; private set; }
    public int Lives { get; private set; } = 3;
    public int Wave => Score / 150 + 1;
    public double AnimationClock => _animationClock;

    public SpaceGame()
    {
        for (int i = 0; i < 105; i++)
        {
            Stars.Add(new Star(
                _random.NextDouble() * FieldWidth,
                _random.NextDouble() * FieldHeight,
                0.8 + _random.NextDouble() * 2.2,
                18 + _random.NextDouble() * 75,
                _random.Next(3)));
        }
    }

    public void Start()
    {
        if (Status == ArcadeStatus.GameOver)
        {
            Reset();
        }

        Status = ArcadeStatus.Playing;
    }

    public void TogglePause()
    {
        if (Status == ArcadeStatus.Playing)
        {
            Status = ArcadeStatus.Paused;
        }
        else if (Status == ArcadeStatus.Paused)
        {
            Status = ArcadeStatus.Playing;
        }
    }

    public void Reset()
    {
        Projectiles.Clear();
        Enemies.Clear();
        Particles.Clear();
        Score = 0;
        Lives = 3;
        PlayerX = FieldWidth / 2;
        PlayerY = FieldHeight - 55;
        InvulnerabilitySeconds = 0;
        _shotCooldown = 0;
        _spawnCooldown = 0.45;
        Status = ArcadeStatus.Ready;
    }

    public void Update(double elapsedSeconds, HashSet<Key> keys)
    {
        double delta = Math.Clamp(elapsedSeconds, 0, 0.05);
        if (Status != ArcadeStatus.Paused)
        {
            _animationClock += delta;
            UpdateStars(delta);
        }

        if (Status != ArcadeStatus.Playing)
        {
            return;
        }

        InvulnerabilitySeconds = Math.Max(0, InvulnerabilitySeconds - delta);
        MovePlayer(delta, keys);
        UpdatePlayerWeapons(delta, keys);
        UpdateEnemies(delta);
        UpdateProjectiles(delta);
        UpdateParticles(delta);
    }

    private void UpdateStars(double delta)
    {
        foreach (Star star in Stars)
        {
            star.Y += star.Speed * delta;
            if (star.Y > FieldHeight)
            {
                star.Y = 0;
                star.X = _random.NextDouble() * FieldWidth;
            }
        }
    }

    private void MovePlayer(double delta, HashSet<Key> keys)
    {
        double horizontal = (keys.Contains(Key.D) || keys.Contains(Key.Right) ? 1 : 0)
                            - (keys.Contains(Key.A) || keys.Contains(Key.Left) ? 1 : 0);
        double vertical = (keys.Contains(Key.S) || keys.Contains(Key.Down) ? 1 : 0)
                          - (keys.Contains(Key.W) || keys.Contains(Key.Up) ? 1 : 0);

        if (horizontal != 0 && vertical != 0)
        {
            horizontal *= 0.7071;
            vertical *= 0.7071;
        }

        PlayerX = Math.Clamp(PlayerX + horizontal * 340 * delta, 30, FieldWidth - 30);
        PlayerY = Math.Clamp(PlayerY + vertical * 300 * delta, 40, FieldHeight - 30);
    }

    private void UpdatePlayerWeapons(double delta, HashSet<Key> keys)
    {
        _shotCooldown = Math.Max(0, _shotCooldown - delta);
        if (!keys.Contains(Key.Space) || _shotCooldown > 0)
        {
            return;
        }

        Projectiles.Add(new Projectile(PlayerX, PlayerY - 22, 0, -590, false));
        _shotCooldown = 0.19;
    }

    private void UpdateEnemies(double delta)
    {
        _spawnCooldown -= delta;
        if (_spawnCooldown <= 0)
        {
            double x = 45 + _random.NextDouble() * (FieldWidth - 90);
            Enemies.Add(new EnemyShip(x, -30, 82 + Wave * 8 + _random.NextDouble() * 55,
                18 + _random.NextDouble() * 5, _random.NextDouble() * 1.7 + 1.4));
            _spawnCooldown = Math.Max(0.38, 1.05 - (Wave - 1) * 0.055) + _random.NextDouble() * 0.32;
        }

        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            EnemyShip enemy = Enemies[i];
            enemy.Phase += delta * 2.5;
            enemy.X += Math.Sin(enemy.Phase) * 43 * delta;
            enemy.Y += enemy.Speed * delta;
            enemy.ShotCooldown -= delta;

            if (enemy.ShotCooldown <= 0 && enemy.Y > 20)
            {
                double dx = PlayerX - enemy.X;
                double dy = PlayerY - enemy.Y;
                double length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                Projectiles.Add(new Projectile(enemy.X, enemy.Y + 10,
                    dx / length * 210, dy / length * 210, true));
                enemy.ShotCooldown = _random.NextDouble() * 0.9 + 1.55;
            }

            double playerDistance = DistanceSquared(enemy.X, enemy.Y, PlayerX, PlayerY);
            if (playerDistance < 27 * 27)
            {
                Enemies.RemoveAt(i);
                CreateExplosion(enemy.X, enemy.Y, true);
                DamagePlayer();
            }
            else if (enemy.Y > FieldHeight + 20)
            {
                Enemies.RemoveAt(i);
                DamagePlayer();
            }
        }
    }

    private void UpdateProjectiles(double delta)
    {
        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = Projectiles[i];
            projectile.X += projectile.VelocityX * delta;
            projectile.Y += projectile.VelocityY * delta;

            if (projectile.Y < -20 || projectile.Y > FieldHeight + 20
                || projectile.X < -20 || projectile.X > FieldWidth + 20)
            {
                Projectiles.RemoveAt(i);
                continue;
            }

            if (projectile.IsEnemy)
            {
                if (DistanceSquared(projectile.X, projectile.Y, PlayerX, PlayerY) < 22 * 22)
                {
                    Projectiles.RemoveAt(i);
                    DamagePlayer();
                }

                continue;
            }

            for (int enemyIndex = Enemies.Count - 1; enemyIndex >= 0; enemyIndex--)
            {
                EnemyShip enemy = Enemies[enemyIndex];
                double hitRadius = enemy.Radius + 5;
                if (DistanceSquared(projectile.X, projectile.Y, enemy.X, enemy.Y) >= hitRadius * hitRadius)
                {
                    continue;
                }

                Projectiles.RemoveAt(i);
                enemy.Health--;
                CreateExplosion(projectile.X, projectile.Y, false, 4);
                if (enemy.Health <= 0)
                {
                    Enemies.RemoveAt(enemyIndex);
                    Score += 25;
                    CreateExplosion(enemy.X, enemy.Y, true);
                }

                break;
            }
        }
    }

    private void UpdateParticles(double delta)
    {
        for (int i = Particles.Count - 1; i >= 0; i--)
        {
            Particle particle = Particles[i];
            particle.X += particle.VelocityX * delta;
            particle.Y += particle.VelocityY * delta;
            particle.Life -= delta;
            if (particle.Life <= 0)
            {
                Particles.RemoveAt(i);
            }
        }
    }

    private void DamagePlayer()
    {
        if (InvulnerabilitySeconds > 0 || Status != ArcadeStatus.Playing)
        {
            return;
        }

        Lives--;
        InvulnerabilitySeconds = 1.25;
        CreateExplosion(PlayerX, PlayerY, false, 14);
        if (Lives <= 0)
        {
            Status = ArcadeStatus.GameOver;
        }
    }

    private void CreateExplosion(double x, double y, bool large, int? count = null)
    {
        int particleCount = count ?? (large ? 18 : 8);
        for (int i = 0; i < particleCount; i++)
        {
            double angle = _random.NextDouble() * Math.PI * 2;
            double speed = _random.NextDouble() * (large ? 145 : 95) + 35;
            Particles.Add(new Particle(x, y, Math.Cos(angle) * speed, Math.Sin(angle) * speed,
                _random.NextDouble() * 0.35 + 0.2, 1 + _random.NextDouble() * 2.7));
        }
    }

    private static double DistanceSquared(double x1, double y1, double x2, double y2)
    {
        double dx = x1 - x2;
        double dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}

internal sealed class Star(double x, double y, double size, double speed, int tone)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Size { get; } = size;
    public double Speed { get; } = speed;
    public int Tone { get; } = tone;
}

internal sealed class Projectile(double x, double y, double velocityX, double velocityY, bool isEnemy)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double VelocityX { get; } = velocityX;
    public double VelocityY { get; } = velocityY;
    public bool IsEnemy { get; } = isEnemy;
}

internal sealed class EnemyShip(double x, double y, double speed, double radius, double shotCooldown)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Speed { get; } = speed;
    public double Radius { get; } = radius;
    public double ShotCooldown { get; set; } = shotCooldown;
    public double Phase { get; set; }
    public int Health { get; set; } = 1;
}

internal sealed class Particle(double x, double y, double velocityX, double velocityY, double life, double size)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double VelocityX { get; } = velocityX;
    public double VelocityY { get; } = velocityY;
    public double Life { get; set; } = life;
    public double InitialLife { get; } = life;
    public double Size { get; } = size;
}