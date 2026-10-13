using Microsoft.Xna.Framework;

namespace ThreeDValley.Renderer;

internal sealed class FreeCamera {
    private const float MoveSpeed = 4f;
    private const float VerticalSpeed = 4f;
    private const float LookSensitivity = 0.0025f;
    private const float MaxPitch = MathHelper.PiOver2 - 0.01f;

    private Vector3 position = new(0f, 0f, 3f);
    private float yaw;
    private float pitch;

    internal void Move(float forward, float strafe, float vertical, float elapsedSeconds) {
        Vector3 forwardDirection = new(MathF.Sin(this.yaw), 0f, -MathF.Cos(this.yaw));
        Vector3 rightDirection = new(MathF.Cos(this.yaw), 0f, MathF.Sin(this.yaw));

        this.position += forwardDirection * (forward * MoveSpeed * elapsedSeconds);
        this.position += rightDirection * (strafe * MoveSpeed * elapsedSeconds);
        this.position.Y += vertical * VerticalSpeed * elapsedSeconds;
    }

    internal void Look(float deltaX, float deltaY) {
        this.yaw += deltaX * LookSensitivity;
        this.pitch = MathHelper.Clamp(this.pitch - deltaY * LookSensitivity, -MaxPitch, MaxPitch);
    }

    internal Matrix GetView() {
        Vector3 direction = new(
            MathF.Cos(this.pitch) * MathF.Sin(this.yaw),
            MathF.Sin(this.pitch),
            -MathF.Cos(this.pitch) * MathF.Cos(this.yaw));

        return Matrix.CreateLookAt(this.position, this.position + direction, Vector3.Up);
    }
}
