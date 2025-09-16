using Image.Otp.Extensions;
using Image.Otp.Primitives;

var test = ImageExtensions.Load<Rgba32>("C:\\Users\\User\\source\\repos\\images\\firstJpg-progressive.jpg");

test.SaveAsBmp("C:\\Users\\User\\source\\repos\\images\\progressive.bmp");
