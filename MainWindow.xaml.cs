using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GalaxyArcadeWPF;

public partial class MainWindow : Window
{
    private static readonly Brush[] StarBrushes =
    {
        FrozenBrush(Color.FromRgb(78, 111, 170)),
        FrozenBrush(Color.FromRgb(113, 141, 195)),
        FrozenBrush(Color.FromRgb(72, 242, 227))
    };

    private static readonly Brush CyanBrush = FrozenBrush(Color.FromRgb(72, 242, 227));
    private static readonly Brush PinkBrush = FrozenBrush(Color.FromRgb(255, 91, 145));
    private static readonly Brush WhiteBrush = FrozenBrush(Color.FromRgb(239, 245, 255));
    private static readonly Brush GridBrush = FrozenBrush(Color.FromRgb(25, 42, 72));
    private static readonly Brush ParticleBrush = FrozenBrush(Color.FromRgb(255, 180, 103));

    private readonly SpaceGame _game = new();
    private readonly HashSet<Key> _heldKeys = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = new();
    private double _lastFrameTime;
    private int _bestScore;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _timer.Stop();
        RenderGame();
        UpdateHud();
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();
        _clock.Start();
        _lastFrameTime = _clock.Elapsed.TotalSeconds;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        double currentTime = _clock.Elapsed.TotalSeconds;
        double delta = currentTime - _lastFrameTime;
        _lastFrameTime = currentTime;
        _game.Update(delta, _heldKeys);
        RenderGame();
        UpdateHud();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        _heldKeys.Add(e.Key);
        if (e.IsRepeat)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                StartMission();
                break;
            case Key.P:
                _game.TogglePause();
                break;
            case Key.R:
                RestartMission();
                break;
            case Key.Space when _game.Status == ArcadeStatus.Ready:
                _game.Start();
                break;
        }

        if (e.Key is Key.Enter or Key.P or Key.R or Key.Space or Key.Up or Key.Down or Key.Left or Key.Right)
        {
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        _heldKeys.Remove(e.Key);
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartMission();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _game.TogglePause();
        Focus();
        RenderGame();
        UpdateHud();
    }

    private void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        RestartMission();
    }

    private void StartMission()
    {
        _game.Start();
        Focus();
        RenderGame();
        UpdateHud();
    }

    private void RestartMission()
    {
        _heldKeys.Clear();
        _game.Reset();
        _game.Start();
        Focus();
        RenderGame();
        UpdateHud();
    }

    private void UpdateHud()
    {
        _bestScore = Math.Max(_bestScore, _game.Score);
        ScoreText.Text = _game.Score.ToString("D6");
        BestScoreText.Text = _bestScore.ToString("D6");
        WaveText.Text = _game.Wave.ToString("D2");
        MissionWaveText.Text = _game.Wave.ToString("D2");
        LivesText.Text = $"{_game.Lives:D2}  /  03";
        StateText.Text = _game.Status switch
        {
            ArcadeStatus.Ready => "READY TO LAUNCH",
            ArcadeStatus.Playing => "IN FLIGHT",
            ArcadeStatus.Paused => "FLIGHT PAUSED",
            ArcadeStatus.GameOver => "MISSION FAILED",
            _ => "READY TO LAUNCH"
        };
        StartButton.Content = _game.Status == ArcadeStatus.GameOver ? "PLAY AGAIN" : "LAUNCH GAME";
        PauseButton.Content = _game.Status == ArcadeStatus.Paused ? "RESUME FLIGHT" : "PAUSE FLIGHT";
        PauseButton.IsEnabled = _game.Status is ArcadeStatus.Playing or ArcadeStatus.Paused;
    }

    private void RenderGame()
    {
        GameCanvas.Children.Clear();
        DrawGrid();

        foreach (Star star in _game.Stars)
        {
            double twinkle = 0.52 + 0.35 * Math.Sin(_game.AnimationClock * 2 + star.X * 0.03);
            AddEllipse(star.X, star.Y, star.Size, star.Size, StarBrushes[star.Tone], twinkle);
        }

        foreach (Particle particle in _game.Particles)
        {
            double opacity = Math.Clamp(particle.Life / particle.InitialLife, 0, 1);
            AddEllipse(particle.X, particle.Y, particle.Size, particle.Size, ParticleBrush, opacity);
        }

        foreach (Projectile projectile in _game.Projectiles)
        {
            Brush brush = projectile.IsEnemy ? PinkBrush : CyanBrush;
            AddEllipse(projectile.X, projectile.Y, projectile.IsEnemy ? 7 : 5,
                projectile.IsEnemy ? 12 : 17, brush, 0.95);
            AddEllipse(projectile.X, projectile.Y, projectile.IsEnemy ? 3 : 2,
                projectile.IsEnemy ? 8 : 13, WhiteBrush, 0.9);
        }

        foreach (EnemyShip enemy in _game.Enemies)
        {
            DrawEnemy(enemy);
        }

        if (_game.Status != ArcadeStatus.GameOver
            && (_game.InvulnerabilitySeconds <= 0 || (int)(_game.AnimationClock * 18) % 2 == 0))
        {
            DrawPlayer();
        }

        if (_game.Status != ArcadeStatus.Playing)
        {
            DrawOverlay();
        }
    }

    private void DrawGrid()
    {
        for (int x = 0; x <= SpaceGame.FieldWidth; x += 60)
        {
            AddLine(x, 0, x, SpaceGame.FieldHeight, GridBrush, 0.21, 1);
        }

        for (int y = 0; y <= SpaceGame.FieldHeight; y += 60)
        {
            AddLine(0, y, SpaceGame.FieldWidth, y, GridBrush, 0.21, 1);
        }
    }

    private void DrawPlayer()
    {
        double x = _game.PlayerX;
        double y = _game.PlayerY;
        AddEllipse(x, y + 9, 48, 34, CyanBrush, 0.1);
        AddPolygon(new[]
        {
            new Point(x, y - 25),
            new Point(x - 20, y + 17),
            new Point(x - 5, y + 11),
            new Point(x, y + 16),
            new Point(x + 5, y + 11),
            new Point(x + 20, y + 17)
        }, CyanBrush, WhiteBrush, 1.3);
        AddPolygon(new[]
        {
            new Point(x, y - 15),
            new Point(x - 5, y + 1),
            new Point(x + 5, y + 1)
        }, FrozenBrush(Color.FromRgb(108, 202, 255)), null, 0);
        AddEllipse(x, y + 6, 5, 9, WhiteBrush, 0.95);
    }

    private void DrawEnemy(EnemyShip enemy)
    {
        double x = enemy.X;
        double y = enemy.Y;
        double r = enemy.Radius;
        AddEllipse(x, y, r * 2.6, r * 1.8, PinkBrush, 0.09);
        AddPolygon(new[]
        {
            new Point(x - r, y - r * 0.15),
            new Point(x - r * 0.62, y - r * 0.72),
            new Point(x - r * 0.2, y - r * 0.42),
            new Point(x + r * 0.2, y - r * 0.42),
            new Point(x + r * 0.62, y - r * 0.72),
            new Point(x + r, y - r * 0.15),
            new Point(x + r * 0.7, y + r * 0.1),
            new Point(x + r * 0.78, y + r * 0.68),
            new Point(x + r * 0.33, y + r * 0.45),
            new Point(x - r * 0.33, y + r * 0.45),
            new Point(x - r * 0.78, y + r * 0.68),
            new Point(x - r * 0.7, y + r * 0.1)
        }, PinkBrush, FrozenBrush(Color.FromRgb(255, 186, 212)), 1);
        AddEllipse(x, y + 1, r * 0.58, r * 0.45, WhiteBrush, 0.85);
    }

    private void DrawOverlay()
    {
        Rectangle shade = new()
        {
            Width = SpaceGame.FieldWidth,
            Height = SpaceGame.FieldHeight,
            Fill = FrozenBrush(Color.FromArgb(190, 4, 8, 21))
        };
        GameCanvas.Children.Add(shade);

        string title = _game.Status switch
        {
            ArcadeStatus.Ready => "DEFEND THE GALAXY",
            ArcadeStatus.Paused => "FLIGHT PAUSED",
            ArcadeStatus.GameOver => "MISSION FAILED",
            _ => string.Empty
        };
        string subtitle = _game.Status switch
        {
            ArcadeStatus.Ready => "PRESS ENTER OR LAUNCH GAME TO BEGIN",
            ArcadeStatus.Paused => "PRESS P OR RESUME FLIGHT TO CONTINUE",
            ArcadeStatus.GameOver => $"FINAL SCORE  {_game.Score:D6}     ·     PRESS R TO REDEPLOY",
            _ => string.Empty
        };

        AddText(title, 450, 257, 36, CyanBrush, true);
        AddText(subtitle, 450, 317, 12, WhiteBrush, false);
    }

    private void AddEllipse(double x, double y, double width, double height, Brush fill, double opacity)
    {
        Ellipse ellipse = new()
        {
            Width = width,
            Height = height,
            Fill = fill,
            Opacity = opacity
        };
        Canvas.SetLeft(ellipse, x - width / 2);
        Canvas.SetTop(ellipse, y - height / 2);
        GameCanvas.Children.Add(ellipse);
    }

    private void AddPolygon(IEnumerable<Point> points, Brush fill, Brush? stroke, double thickness)
    {
        Polygon polygon = new()
        {
            Points = new PointCollection(points),
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round
        };
        GameCanvas.Children.Add(polygon);
    }

    private void AddLine(double x1, double y1, double x2, double y2, Brush stroke, double opacity, double thickness)
    {
        Line line = new()
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = stroke,
            Opacity = opacity,
            StrokeThickness = thickness
        };
        GameCanvas.Children.Add(line);
    }

    private void AddText(string text, double centerX, double top, double fontSize, Brush brush, bool bold)
    {
        TextBlock block = new()
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = brush,
            TextAlignment = TextAlignment.Center
        };
        block.Measure(new Size(SpaceGame.FieldWidth, SpaceGame.FieldHeight));
        Canvas.SetLeft(block, centerX - block.DesiredSize.Width / 2);
        Canvas.SetTop(block, top);
        GameCanvas.Children.Add(block);
    }
}