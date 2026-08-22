using Herculan.Engine.Numerics;
using Xunit;

namespace Herculan.Engine.Tests;

/// <summary>
/// Checks the ported fixed-point transform against ordinary floating-point trigonometry. The port
/// reproduces DBSIM's own quantization (angles snap to 1/4096 of a turn, products round in Q14), so
/// the tolerances below are the quantization, not slack.
/// </summary>
public class Transform3Tests {
	private const double TurnsPerBam = 1.0 / BinaryAngle.FullTurn;

	[Theory]
	[InlineData(0)]
	[InlineData(1820)]
	[InlineData(8192)]
	[InlineData(16384)]
	[InlineData(30000)]
	[InlineData(-8192)]
	[InlineData(-30000)]
	public void CosAndSinMatchRealTrigonometry(int angle) {
		double radians = angle * TurnsPerBam * 2 * System.Math.PI;

		Assert.InRange(SimTrig.Cos((short)angle) / (double)SimTrig.One,
			System.Math.Cos(radians) - 0.002, System.Math.Cos(radians) + 0.002);
		Assert.InRange(SimTrig.Sin((short)angle) / (double)SimTrig.One,
			System.Math.Sin(radians) - 0.002, System.Math.Sin(radians) + 0.002);
	}

	[Theory]
	[InlineData(1, 0, 0)]
	[InlineData(0, 1, 0x4000)]
	[InlineData(-1, 0, 0x8000)]
	[InlineData(1, 1, 0x2000)]
	[InlineData(-1, -1, 0xa000)]
	public void Atan2PlacesTheRightQuadrant(int x, int y, int expected) {
		Assert.Equal(expected & 0xffff, SimTrig.Atan2(y * 1000, x * 1000) & 0xffff);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(4096)]
	[InlineData(8192)]
	[InlineData(16384)]
	[InlineData(-8192)]
	public void AsinInvertsSin(int angle) {
		short sine = SimTrig.Sin((short)angle);
		Assert.InRange(SimTrig.Asin(sine), angle - 8, angle + 8);
	}

	[Fact]
	public void ZOnlyRotationTakesTheCheapShape() {
		var transform = Transform3.FromEuler(0, 0, 0x2000);

		Assert.Equal(Transform3.KindZOnly, transform.Kind);
		Assert.Equal(SimTrig.One, transform.M[8]);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1820)]
	[InlineData(0x4000)]
	[InlineData(0x9000)]
	public void RotatingForwardMatchesTheHeadingConvention(int heading) {
		// Model forward is +Y. A heading of zero points along world +Y, and increasing it turns the
		// machine anticlockwise -- toward -X -- which is what MissionLoader's formation spread and
		// the render transform both assume.
		var transform = Transform3.FromEuler(0, 0, (short)heading);
		var forward = transform.RotateVector(0, 10000, 0);

		double radians = heading * TurnsPerBam * 2 * System.Math.PI;
		Assert.InRange(forward.X, -10000 * System.Math.Sin(radians) - 30,
			-10000 * System.Math.Sin(radians) + 30);
		Assert.InRange(forward.Y, 10000 * System.Math.Cos(radians) - 30,
			10000 * System.Math.Cos(radians) + 30);
	}

	[Fact]
	public void ConcatAppliesTheFirstArgumentFirst() {
		var first = Transform3.FromEuler(0, 0, 0x4000);
		first.X = 1000;

		var second = Transform3.Identity;
		second.Y = 500;

		var combined = Transform3.Concat(first, second);
		var direct = second.TransformPoint(first.X, first.Y, first.Z);

		Assert.Equal(direct.X, combined.X);
		Assert.Equal(direct.Y, combined.Y);
	}

	[Fact]
	public void ConcatWithAnInverseCancels() {
		var rotation = Transform3.FromEuler(0, 0, 0x1234);
		rotation.X = 700;
		rotation.Y = -300;
		rotation.Z = 40;

		var inverse = rotation;
		inverse.TransposeRotation();
		var moved = inverse.RotateVector(-rotation.X, -rotation.Y, -rotation.Z);
		inverse.X = moved.X;
		inverse.Y = moved.Y;
		inverse.Z = moved.Z;

		var identity = Transform3.Concat(rotation, inverse);

		Assert.InRange(identity.X, -2, 2);
		Assert.InRange(identity.Y, -2, 2);
		Assert.InRange(identity.Z, -2, 2);
		Assert.InRange(identity.M[0], SimTrig.One - 2, SimTrig.One + 2);
	}

	[Fact]
	public void EulerSurvivesARoundTrip() {
		var transform = Transform3.FromEuler(0, 0, 0x3000);
		var (x, y, z) = transform.ToEuler();

		Assert.InRange(x, -8, 8);
		Assert.InRange(y, -8, 8);
		Assert.InRange(z, 0x3000 - 8, 0x3000 + 8);
	}
}
