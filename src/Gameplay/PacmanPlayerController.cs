using System;
using com.radiant.engine.bundle;
using com.radiant.engine.core;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;

namespace RaytracingPacman;

[Pausable(PauseGroup.Gameplay | PauseGroup.Animation)]
[RunAfter(typeof(PacmanMazeBuilder))]
[RunBefore(typeof(PacmanGhostAI))]
[SystemTag("Pacman")]
public class PacmanPlayerController : com.radiant.engine.core.System
{
    public (int x, int y) Cell { get; private set; }
    public Vector2 WorldPosition { get; private set; }
    public int CoinsCollected { get; private set; }
    public int CoinsTotal { get; private set; }
    public Color CoinColor { get; set; } = new Color(255, 220, 50);
    public bool PlayerCaught { get; set; }
    public bool HasMoved { get; private set; }
    public PacmanGhostAI GhostAI { get; set; }
    public float FrightenedDuration { get; set; } = 5f;
    public float HitboxRadius { get; set; } = 10f;
    public string LevelTag { get; set; } = "1-1";

    public int EntityId { get; private set; }
    public bool IsTracked { get; private set; }
    public Vector2 RenderPosition { get; private set; }
    public float WobbleX { get; private set; }
    public float WobbleY { get; private set; }
    public (int dx, int dy) FacingDirection { get; private set; } = (1, 0);
    public bool MouthOpen { get; private set; }
    public float MouthRotation { get; private set; }
    public float BodyRadius { get; private set; }
    public float CollectFlash { get; private set; }

    const int RenderTargetSize = 128;
    static readonly Color PlayerEyeColor = new Color(30, 15, 5);

    RenderTarget2D PlayerRenderTarget;
    Texture2D CircleTexture;
    Texture2D MouthTexture;
    Texture2D EyesTexture;
    Geometry Geometry;
    bool PlayerHidden;
    Color SavedAlbedo;
    Color SavedEmissive;

    PacmanMazeBuilder Maze;
    public float Speed { get; set; } = 200f;
    float Depth;
    (int x, int y) PreviousCell = (-1, -1);

    (int x, int y) TargetCell;
    (int dx, int dy) CurrentDirection;
    (int dx, int dy) BufferedDirection;

    float MouthTimer;
    Vector2 PreviousRenderPosition;
    float IdleTime;

    public void Track(int entityId, (int x, int y) startCell, float depth)
    {
        EntityId = entityId;
        Cell = startCell;
        TargetCell = startCell;
        CurrentDirection = (0, 0);
        BufferedDirection = (0, 0);
        Depth = depth;
        WorldPosition = Maze.CellCenter(startCell.x, startCell.y);

        IsTracked = true;
        PlayerCaught = false;
        HasMoved = false;
        PreviousCell = startCell;
        CoinsCollected = 0;
        CoinsTotal = Maze.CoinCells.Count;

        RenderPosition = WorldPosition;
        PreviousRenderPosition = WorldPosition;
        FacingDirection = (1, 0);
        MouthTimer = 0f;
        MouthOpen = true;
        MouthRotation = 0f;
        CollectFlash = 0f;
        BodyRadius = Scene.ECS.GetComponent<Circle2D>(entityId).Radius;

        ref var material = ref Scene.ECS.GetComponent<Material>(entityId);
        material.Texture = PlayerRenderTarget;
    }

    public void Clear()
    {
        if (!IsTracked) return;

        Scene.ECS.DestroyEntity(EntityId);
        IsTracked = false;
        CoinsCollected = 0;
        CoinsTotal = 0;
    }

    public void HidePlayer()
    {
        if (PlayerHidden || !IsTracked || !Scene.ECS.IsAlive(EntityId)) return;

        ref var material = ref Scene.ECS.GetComponent<Material>(EntityId);
        SavedAlbedo = material.Albedo;
        SavedEmissive = material.Emissive;

        material.Albedo = Color.Transparent;
        material.Emissive = Color.Transparent;
        PlayerHidden = true;
    }

    public void ShowPlayer()
    {
        if (!PlayerHidden || !IsTracked || !Scene.ECS.IsAlive(EntityId)) return;

        ref var material = ref Scene.ECS.GetComponent<Material>(EntityId);
        material.Albedo = SavedAlbedo;
        material.Emissive = SavedEmissive;
        PlayerHidden = false;
    }

    public override void Initialize()
    {
        Maze = Scene.ECS.GetSystem<PacmanMazeBuilder>();
        Geometry = Scene.ECS.GetSystem<Geometry>();

        EyesTexture = Renderer.GetTexture("Eyes");
        MouthTexture = CreateMouthTexture(RenderTargetSize);
        CircleTexture = Renderer.GetCircleTexture(RenderTargetSize);
        PlayerRenderTarget = Renderer.CreateRenderTarget(RenderTargetSize, RenderTargetSize);
    }

    public override void Update()
    {
        if (Maze == null || !IsTracked) return;
        if (Scene.ECS.IsDisabled(EntityId)) return;

        RenderPosition = PreviousRenderPosition;
        PreviousRenderPosition = WorldPosition;

        var keyboardState = Keyboard.GetState();
        ReadInput(keyboardState);

        float deltaTime = (float)GameTime.ElapsedGameTime.TotalSeconds;
        float moveDistance = Speed * deltaTime;

        if (!Scene.ECS.IsAlive(EntityId)) return;

        ref var transform = ref Scene.ECS.GetComponent<Transform>(EntityId);

        var position = WorldPosition;
        var targetPosition = Maze.CellCenter(TargetCell.x, TargetCell.y);

        var targetOffset = targetPosition - position;
        float distanceToTarget = targetOffset.Length();

        if (distanceToTarget <= moveDistance)
        {
            var unwrappedTargetCell = TargetCell;
            Cell = (Maze.WrapX(unwrappedTargetCell.x), unwrappedTargetCell.y);
            position = Maze.CellCenter(Cell.x, Cell.y);

            if (Cell != PreviousCell)
            {
                if (!HasMoved) HasMoved = true;
                CollectAt(Cell.x, Cell.y);
                PreviousCell = Cell;
            }

            if (BufferedDirection != (0, 0) && Maze.CanMove(Cell.x, Cell.y, BufferedDirection.dx, BufferedDirection.dy))
            {
                CurrentDirection = BufferedDirection;
                BufferedDirection = (0, 0);
            }

            if (CurrentDirection != (0, 0) && Maze.CanMove(Cell.x, Cell.y, CurrentDirection.dx, CurrentDirection.dy))
            {
                TargetCell = (Cell.x + CurrentDirection.dx, Cell.y + CurrentDirection.dy);
            }
            else
            {
                CurrentDirection = (0, 0);
                TargetCell = Cell;
            }

            targetPosition = Maze.CellCenter(TargetCell.x, TargetCell.y);
            targetOffset = targetPosition - position;
            distanceToTarget = targetOffset.Length();
        }

        if (distanceToTarget > 0.01f)
        {
            var movementOffset = targetOffset / distanceToTarget * MathF.Min(moveDistance, distanceToTarget);
            position += movementOffset;
        }

        IdleTime += deltaTime;
        float wobble = MathF.Sin(IdleTime * 3f) * 3f;
        WobbleX = CurrentDirection.dy != 0 ? wobble : 0f;
        WobbleY = CurrentDirection.dy != 0 ? 0f : wobble;
        transform.Position = new Vector3(position.X + WobbleX, position.Y + WobbleY, Depth);
        WorldPosition = position;

        if (CurrentDirection != (0, 0))
        {
            int aheadCellX = Maze.WrapX(Cell.x + CurrentDirection.dx);
            int aheadCellY = Cell.y + CurrentDirection.dy;
            var aheadCellCenter = Maze.CellCenter(aheadCellX, aheadCellY);
            float aheadOffsetX = position.X - aheadCellCenter.X;
            float aheadOffsetY = position.Y - aheadCellCenter.Y;
            float reachThreshold = Maze.CellSize * 0.425f;
            if (aheadOffsetX * aheadOffsetX + aheadOffsetY * aheadOffsetY < reachThreshold * reachThreshold)
                CollectAt(aheadCellX, aheadCellY);
        }

        if (CollectFlash > 0f)
            CollectFlash = MathF.Max(0f, CollectFlash - deltaTime * 4f);

        ref var circle = ref Scene.ECS.GetComponent<Circle2D>(EntityId);
        circle.Radius = BodyRadius * (1f + CollectFlash * 0.20f);

        if (CurrentDirection != (0, 0))
        {
            FacingDirection = CurrentDirection;
            MouthTimer += deltaTime * 24f;
        }

        float phase = (MathF.Sin(MouthTimer) + 1f) / 2f;
        MouthOpen = CurrentDirection == (0, 0) || phase > 0.3f;
        MouthRotation = (FacingDirection.dx, FacingDirection.dy) switch
        {
            (1, 0) => 0f,
            (0, 1) => MathF.PI / 2f,
            (-1, 0) => MathF.PI,
            (0, -1) => -MathF.PI / 2f,
            _ => 0f
        };

        RenderPlayerTexture();
    }

    void ReadInput(KeyboardState keyboardState)
    {
        (int dx, int dy) desiredDirection = (0, 0);

        if (keyboardState.IsKeyDown(Keys.Up)) desiredDirection = (0, -1);
        else if (keyboardState.IsKeyDown(Keys.Left)) desiredDirection = (-1, 0);
        else if (keyboardState.IsKeyDown(Keys.Down)) desiredDirection = (0, 1);
        else if (keyboardState.IsKeyDown(Keys.Right)) desiredDirection = (1, 0);

        if (desiredDirection == (0, 0)) return;

        if (Maze.CanMove(Cell.x, Cell.y, desiredDirection.dx, desiredDirection.dy))
        {
            if (desiredDirection.dx == -CurrentDirection.dx && desiredDirection.dy == -CurrentDirection.dy)
            {
                CurrentDirection = desiredDirection;
                TargetCell = (Cell.x + desiredDirection.dx, Cell.y + desiredDirection.dy);
                BufferedDirection = (0, 0);
                return;
            }

            BufferedDirection = desiredDirection;
        }
        else
        {
            BufferedDirection = desiredDirection;
        }
    }

    void CollectAt(int cellX, int cellY)
    {
        if (Maze.TryCollectCoin(cellX, cellY))
        {
            CoinsCollected++;
            CollectFlash = 1f;
        }

        if (Maze.TryCollectPowerPellet(cellX, cellY))
        {
            GhostAI?.SetFrightened(FrightenedDuration);
            GhostAI?.SetRainbowFrightened(FrightenedDuration);
            CollectFlash = 1f;
        }
    }

    public override void LateRender()
    {
        if (Geometry.IsDebugHidingGameplay) return;
        if (Scene.ECS.IsDisabled(EntityId)) return;

        DrawPlayerEyes();
    }

    void RenderPlayerTexture()
    {
        if (PlayerRenderTarget == null || !IsTracked) return;

        Renderer.PushTargets();
        Renderer.SetTarget(PlayerRenderTarget).Clear(Color.Transparent);

        Renderer.BeginDraw(SpriteSortMode.Immediate, BlendState.AlphaBlend);

        Renderer.DrawSprite(CircleTexture, new Rectangle(0, 0, RenderTargetSize, RenderTargetSize), Color.White);

        Renderer.EndDraw();

        if (MouthOpen)
        {
            int halfSize = RenderTargetSize / 2;
            var origin = new Vector2(halfSize, halfSize);
            Renderer.BlitMask(MouthTexture, new Rectangle(halfSize, halfSize, RenderTargetSize, RenderTargetSize), MouthRotation, origin);
        }

        Renderer.PopTargets();
    }

    void DrawPlayerEyes()
    {
        if (!IsTracked) return;
        if (!Scene.ECS.IsAlive(EntityId)) return;

        float radius = Scene.ECS.GetComponent<Circle2D>(EntityId).Radius;
        var facingDirection = FacingDirection;

        if (EyesTexture == null || facingDirection.dy != 0) return;

        float playerCenterX = RenderPosition.X + WobbleX;
        float playerCenterY = RenderPosition.Y + WobbleY;

        float eyeRadius = radius * 0.767f;
        float eyeDiameter = eyeRadius * 2f;
        float eyeOffset = radius * 0.133f;
        float eyeCenterX = playerCenterX + facingDirection.dx * eyeOffset;
        float eyeCenterY = playerCenterY + facingDirection.dy * eyeOffset - radius * 0.1f;
        int textureWidth = EyesTexture.Width;
        int textureHeight = EyesTexture.Height;

        Renderer.BeginDraw(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        if (facingDirection.dx == 1)
        {
            var sourceRectangle = new Rectangle(0, 0, textureWidth / 2, textureHeight);
            var destinationRectangle = Renderer.VirtualToScreenRect(eyeCenterX - eyeRadius, eyeCenterY - eyeRadius, eyeRadius, eyeDiameter);
            Renderer.DrawSprite(EyesTexture, destinationRectangle, sourceRectangle, PlayerEyeColor, 0f, Vector2.Zero);
        }
        else if (facingDirection.dx == -1)
        {
            var sourceRectangle = new Rectangle(textureWidth / 2, 0, textureWidth / 2, textureHeight);
            var destinationRectangle = Renderer.VirtualToScreenRect(eyeCenterX, eyeCenterY - eyeRadius, eyeRadius, eyeDiameter);
            Renderer.DrawSprite(EyesTexture, destinationRectangle, sourceRectangle, PlayerEyeColor, 0f, Vector2.Zero);
        }

        Renderer.EndDraw();
    }

    Texture2D CreateMouthTexture(int size)
    {
        var texture = Renderer.CreateTexture(size, size, SurfaceFormat.Color);
        var pixels = new Color[size * size];
        float halfSize = size / 2f;
        float originX = halfSize - 4f;
        float slope = MathF.Tan(MathF.PI * 0.22f);

        for (int pixelY = 0; pixelY < size; pixelY++)
        {
            for (int pixelX = 0; pixelX < size; pixelX++)
            {
                float horizontalOffset = pixelX - originX;
                float verticalOffset = pixelY - halfSize;

                if (horizontalOffset > 0f && MathF.Abs(verticalOffset) < slope * horizontalOffset)
                    pixels[pixelY * size + pixelX] = new Color((byte)0, (byte)0, (byte)0, (byte)255);
            }
        }

        texture.SetData(pixels);
        return texture;
    }
}
