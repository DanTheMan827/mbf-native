using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace ModsBeforeFriday.App.Controls;

/// <summary>
/// Native WinUI/Win2D port of the ModsBeforeFriday falling-block web background.
/// The control is non-interactive and is intended to fill the window behind the app UI.
/// </summary>
public sealed partial class AnimatedBackgroundControl : UserControl, IDisposable
{
    // Original web values converted from pixels/millisecond to DIPs/second.
    private const float BlockFallSpeed = 10.0f;
    private const float BlockRotationSpeed = 0.2f;
    private const float BlockScaleRange = 0.4f;
    private const float BlockBrightnessRange = 0.4f;
    private const float BlockValueSafetyLimit = 0.1f;
    private const float BlockDensity = 0.00002f;
    private const float DensityRefreshSeconds = 0.5f;

    // CSS filter: blur(0.5em), assuming the normal 16-DIP root font size.
    private const float ParticleBlurAmount = 8.0f;

    private static readonly Color BackgroundColor = Color.FromArgb(255, 17, 17, 34);
    private static readonly Color BlueBlockFill = Color.FromArgb(255, 0, 0, 120);
    private static readonly Color BlueBlockStroke = Color.FromArgb(255, 0, 0, 80);
    private static readonly Color BlueAccentStroke = Color.FromArgb(255, 0, 97, 255);
    private static readonly Color BlueAccentFill = Color.FromArgb(255, 185, 186, 255);
    private static readonly Color RedBlockFill = Color.FromArgb(255, 120, 0, 0);
    private static readonly Color RedBlockStroke = Color.FromArgb(255, 80, 0, 0);
    private static readonly Color RedAccentStroke = Color.FromArgb(255, 255, 0, 0);
    private static readonly Color RedAccentFill = Color.FromArgb(255, 255, 232, 232);
    private static readonly Color Black = Color.FromArgb(255, 0, 0, 0);

    private readonly object _stateGate = new();
    private readonly Random _random = new();
    private readonly List<FallingParticle> _particles = [];
    private readonly UISettings _uiSettings = new();

    private CanvasGeometry? _arrowGeometry;
    private CanvasGeometry? _bombGeometry;
    private CanvasRadialGradientBrush? _bombBrush;

    private float _viewportWidth;
    private float _viewportHeight;
    private float _rasterizationScale = 1.0f;
    private float _densityAccumulator;
    private bool _isLoaded;
    private bool _isAnimationEnabled = true;
    private bool _respectSystemReducedMotion = true;

    public AnimatedBackgroundControl()
    {
        InitializeComponent();

        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(BackgroundColor);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    /// <summary>
    /// Allows application settings to disable the animation while retaining the #111122 background.
    /// </summary>
    public bool IsAnimationEnabled
    {
        get => _isAnimationEnabled;
        set
        {
            if (_isAnimationEnabled == value)
            {
                return;
            }

            _isAnimationEnabled = value;
            UpdateAnimationState();
        }
    }

    /// <summary>
    /// When true, honors Windows' "Animation effects" accessibility setting.
    /// </summary>
    public bool RespectSystemReducedMotion
    {
        get => _respectSystemReducedMotion;
        set
        {
            if (_respectSystemReducedMotion == value)
            {
                return;
            }

            _respectSystemReducedMotion = value;
            UpdateAnimationState();
        }
    }

    private bool ShouldAnimate =>
        _isLoaded &&
        _isAnimationEnabled &&
        (!_respectSystemReducedMotion || _uiSettings.AnimationsEnabled);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        _uiSettings.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
        CaptureViewport();
        UpdateAnimationState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _uiSettings.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
        ParticleCanvas.Paused = true;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        CaptureViewport();
    }

    private void CaptureViewport()
    {
        lock (_stateGate)
        {
            _viewportWidth = Math.Max(0, (float)ActualWidth);
            _viewportHeight = Math.Max(0, (float)ActualHeight);
            _rasterizationScale = (float)(XamlRoot?.RasterizationScale ?? 1.0);
            ReconcileParticleCount(spawnAtTop: false);
        }
    }

    private void OnAnimationsEnabledChanged(
        UISettings sender,
        UISettingsAnimationsEnabledChangedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(UpdateAnimationState);
    }

    private void UpdateAnimationState()
    {
        if (!_isLoaded)
        {
            return;
        }

        ParticleCanvas.Paused = !ShouldAnimate;

        if (ShouldAnimate)
        {
            lock (_stateGate)
            {
                ReconcileParticleCount(spawnAtTop: false);
            }
        }
        else
        {
            // Matches prefers-reduced-motion hiding #anim-bg: keep only the solid body color.
            ParticleCanvas.Invalidate();
        }
    }

    private void ParticleCanvas_CreateResources(
        CanvasAnimatedControl sender,
        CanvasCreateResourcesEventArgs args)
    {
        _arrowGeometry?.Dispose();
        _bombGeometry?.Dispose();
        _bombBrush?.Dispose();

        _arrowGeometry = CreateArrowGeometry(sender);
        _bombGeometry = CreateBombGeometry(sender);

        _bombBrush = new CanvasRadialGradientBrush(
            sender,
            [
                new CanvasGradientStop { Position = 0.0f, Color = Color.FromArgb(255, 20, 20, 20) },
                new CanvasGradientStop { Position = 0.75f, Color = Color.FromArgb(255, 3, 3, 3) },
                new CanvasGradientStop { Position = 1.0f, Color = Color.FromArgb(255, 3, 3, 3) },
            ])
        {
            Center = Vector2.Zero,
            OriginOffset = Vector2.Zero,
            RadiusX = 72,
            RadiusY = 72,
        };
    }

    private void ParticleCanvas_Draw(
        ICanvasAnimatedControl sender,
        CanvasAnimatedDrawEventArgs args)
    {
        args.DrawingSession.Clear(BackgroundColor);

        if (!ShouldAnimate || _viewportWidth <= 0 || _viewportHeight <= 0)
        {
            return;
        }

        float elapsedSeconds = (float)Math.Min(args.Timing.ElapsedTime.TotalSeconds, 0.1);

        lock (_stateGate)
        {
            UpdateParticles(elapsedSeconds);

            using var particleLayer = new CanvasCommandList(sender);
            using (CanvasDrawingSession particleSession = particleLayer.CreateDrawingSession())
            {
                particleSession.Clear(Color.FromArgb(0, 0, 0, 0));

                foreach (FallingParticle particle in _particles)
                {
                    DrawParticle(particleSession, particle);
                }
            }

            var blur = new GaussianBlurEffect
            {
                Source = particleLayer,
                BlurAmount = ParticleBlurAmount,
                BorderMode = EffectBorderMode.Hard,
                Optimization = EffectOptimization.Balanced,
            };

            args.DrawingSession.DrawImage(blur);
        }
    }

    private void UpdateParticles(float elapsedSeconds)
    {
        _densityAccumulator += elapsedSeconds;
        if (_densityAccumulator >= DensityRefreshSeconds)
        {
            _densityAccumulator %= DensityRefreshSeconds;
            ReconcileParticleCount(spawnAtTop: true);
        }

        foreach (FallingParticle particle in _particles)
        {
            particle.Position += particle.Velocity * elapsedSeconds;
            particle.Rotation += particle.AngularVelocity * elapsedSeconds;

            float minValue = 1.0f - BlockValueSafetyLimit - BlockScaleRange;
            float maxValue = 1.0f + BlockValueSafetyLimit + BlockScaleRange;

            particle.Scale = Math.Clamp(
                particle.Scale + particle.ScaleChangeSpeed * elapsedSeconds,
                minValue,
                maxValue);

            particle.Brightness = Math.Clamp(
                particle.Brightness + particle.BrightnessChangeSpeed * elapsedSeconds,
                1.0f - BlockValueSafetyLimit - BlockBrightnessRange,
                1.0f + BlockValueSafetyLimit + BlockBrightnessRange);
        }

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            FallingParticle particle = _particles[i];
            float extent = 100.0f * particle.Scale;

            bool exited =
                particle.Position.Y > _viewportHeight + extent ||
                particle.Position.X < -extent ||
                particle.Position.X > _viewportWidth + extent;

            if (exited)
            {
                RandomizeParticle(particle, startAtTop: true);
            }
        }
    }

    private void ReconcileParticleCount(bool spawnAtTop)
    {
        int desiredCount = CalculateBlockCount();

        while (_particles.Count < desiredCount)
        {
            var particle = new FallingParticle();
            RandomizeParticle(particle, startAtTop: spawnAtTop);
            _particles.Add(particle);
        }

        while (_particles.Count > desiredCount)
        {
            _particles.RemoveAt(_particles.Count - 1);
        }
    }

    private int CalculateBlockCount()
    {
        if (_viewportWidth <= 0 || _viewportHeight <= 0)
        {
            return 0;
        }

        // The web implementation divides by devicePixelRatio. WinUI coordinates are DIPs,
        // but using RasterizationScale here preserves the original high-DPI density behavior.
        float count = _viewportWidth * _viewportHeight * BlockDensity / Math.Max(_rasterizationScale, 1.0f);
        return Math.Max((int)MathF.Ceiling(count), 20);
    }

    private void RandomizeParticle(FallingParticle particle, bool startAtTop)
    {
        // Exact distribution produced by: floor(7 * random) - 2; if (< 0) += 2
        int type = _random.Next(7) - 2;
        if (type < 0)
        {
            type += 2;
        }

        particle.Type = type;
        particle.AngularVelocity = RandomSigned() * BlockRotationSpeed;
        particle.Rotation = 1.0f - RandomSigned() * MathF.PI;

        float start = RandomSigned();
        float end = RandomSigned();

        particle.Scale = 1.0f - start * BlockScaleRange;
        float endScale = 1.0f - end * BlockScaleRange;

        particle.Brightness = 1.0f - start * BlockBrightnessRange;
        float endBrightness = 1.0f - end * BlockBrightnessRange;

        float startXPercentage = NextFloat();
        particle.Position = new Vector2(
            startXPercentage * (_viewportWidth + 200.0f * particle.Scale) - particle.Scale * 100.0f,
            NextFloat() * (_viewportHeight + 150.0f * particle.Scale) - particle.Scale * 100.0f);

        if (startAtTop)
        {
            particle.Position = new Vector2(particle.Position.X, -100.0f * particle.Scale);
        }

        float dropAngle = NextFloat() * (MathF.PI / 2.0f) + MathF.PI / 4.0f;
        particle.Velocity = new Vector2(
            BlockFallSpeed * MathF.Cos(dropAngle),
            BlockFallSpeed * MathF.Sin(dropAngle));

        float verticalSpeed = Math.Max(particle.Velocity.Y, 0.001f);
        float estimatedSeconds = (_viewportHeight - particle.Position.Y + endScale) / verticalSpeed;
        estimatedSeconds = Math.Max(estimatedSeconds, 0.001f);

        particle.ScaleChangeSpeed = (endScale - particle.Scale) / estimatedSeconds;

        // Intentionally preserves the original JS expression rather than "fixing" its sign.
        particle.BrightnessChangeSpeed = particle.Type == 4
            ? 0.0f
            : (particle.Brightness - endBrightness) / estimatedSeconds;
    }

    private float NextFloat() => (float)_random.NextDouble();

    private float RandomSigned() => 2.0f * NextFloat() - 1.0f;

    private void DrawParticle(CanvasDrawingSession ds, FallingParticle particle)
    {
        Matrix3x2 oldTransform = ds.Transform;
        ds.Transform =
            Matrix3x2.CreateScale(particle.Scale) *
            Matrix3x2.CreateRotation(particle.Rotation) *
            Matrix3x2.CreateTranslation(particle.Position);

        try
        {
            switch (particle.Type)
            {
                case 0:
                    DrawBlock(ds, particle.Brightness, isRed: false, useCircle: false);
                    break;

                case 1:
                    DrawBlock(ds, particle.Brightness, isRed: true, useCircle: false);
                    break;

                case 2:
                    DrawBlock(ds, particle.Brightness, isRed: false, useCircle: true);
                    break;

                case 3:
                    DrawBlock(ds, particle.Brightness, isRed: true, useCircle: true);
                    break;

                case 4:
                    DrawBomb(ds);
                    break;
            }
        }
        finally
        {
            ds.Transform = oldTransform;
        }
    }

    private void DrawBlock(CanvasDrawingSession ds, float brightness, bool isRed, bool useCircle)
    {
        Color blockFill = ApplyBrightness(isRed ? RedBlockFill : BlueBlockFill, brightness);
        Color blockStroke = ApplyBrightness(isRed ? RedBlockStroke : BlueBlockStroke, brightness);

        ds.FillRoundedRectangle(-50, -50, 100, 100, 20, 20, blockFill);
        ds.DrawRoundedRectangle(-50, -50, 100, 100, 20, 20, blockStroke, 4);

        if (useCircle)
        {
            // This preserves the original CSS selector behavior: ".red-block path, ellipse"
            // colors every ellipse red, including the ellipse in a blue block.
            ds.FillCircle(Vector2.Zero, 20, ApplyBrightness(RedAccentFill, brightness));
            ds.DrawCircle(Vector2.Zero, 20, ApplyBrightness(RedAccentStroke, brightness), 4);
            return;
        }

        if (_arrowGeometry is null)
        {
            return;
        }

        Color accentFill = ApplyBrightness(isRed ? RedAccentFill : BlueAccentFill, brightness);
        Color accentStroke = ApplyBrightness(isRed ? RedAccentStroke : BlueAccentStroke, brightness);

        ds.FillGeometry(_arrowGeometry, accentFill);
        ds.DrawGeometry(_arrowGeometry, accentStroke, 4);
    }

    private void DrawBomb(CanvasDrawingSession ds)
    {
        if (_bombGeometry is null || _bombBrush is null)
        {
            return;
        }

        ds.FillGeometry(_bombGeometry, _bombBrush);
        ds.DrawGeometry(_bombGeometry, Black, 4);
    }

    private static CanvasGeometry CreateArrowGeometry(ICanvasResourceCreator creator)
    {
        using var builder = new CanvasPathBuilder(creator);
        builder.BeginFigure(-40, -40);
        builder.AddLine(40, -40);
        builder.AddLine(40, -30);
        builder.AddLine(0, -10);
        builder.AddLine(-40, -30);
        builder.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(builder);
    }

    private static CanvasGeometry CreateBombGeometry(ICanvasResourceCreator creator)
    {
        ReadOnlySpan<Vector2> points =
        [
            new(16.588f, 25.261f),
            new(0.271f, 25.261f),
            new(-9.292f, 58.594f),
            new(-8.873f, 25.260f),
            new(-19.645f, 25.260f),
            new(-24.671f, 29.566f),
            new(-21.536f, 21.708f),
            new(-25.928f, 6.658f),
            new(-66.658f, 9.545f),
            new(-28.483f, -2.096f),
            new(-31.320f, -11.818f),
            new(-30.336f, -12.560f),
            new(-41.991f, -20.297f),
            new(-24.148f, -17.223f),
            new(-17.527f, -22.213f),
            new(-32.214f, -56.515f),
            new(-9.796f, -28.040f),
            new(-5.567f, -31.228f),
            new(-2.620f, -47.336f),
            new(0.907f, -33.318f),
            new(13.827f, -23.318f),
            new(46.439f, -48.293f),
            new(21.605f, -17.299f),
            new(26.002f, -13.896f),
            new(39.033f, -14.184f),
            new(27.412f, -7.903f),
            new(24.150f, 2.090f),
            new(60.606f, 22.849f),
            new(21.106f, 11.417f),
            new(18.779f, 18.547f),
            new(25.405f, 33.345f),
            new(16.652f, 25.066f),
            new(16.588f, 25.261f),
        ];

        using var builder = new CanvasPathBuilder(creator);
        builder.BeginFigure(points[0]);

        for (int i = 1; i < points.Length; i++)
        {
            builder.AddLine(points[i]);
        }

        builder.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(builder);
    }

    private static Color ApplyBrightness(Color color, float brightness)
    {
        static byte Scale(byte channel, float amount) =>
            (byte)Math.Clamp(MathF.Round(channel * amount), 0, 255);

        return Color.FromArgb(
            color.A,
            Scale(color.R, brightness),
            Scale(color.G, brightness),
            Scale(color.B, brightness));
    }

    private sealed class FallingParticle
    {
        public int Type { get; set; }
        public Vector2 Position { get; set; }
        public float Rotation { get; set; }
        public float Scale { get; set; } = 1.0f;
        public float Brightness { get; set; } = 1.0f;
        public Vector2 Velocity { get; set; }
        public float AngularVelocity { get; set; }
        public float ScaleChangeSpeed { get; set; }
        public float BrightnessChangeSpeed { get; set; }
    }

    public void Dispose()
    {
        _arrowGeometry?.Dispose();
        _bombGeometry?.Dispose();
        _bombBrush?.Dispose();
    }
}
