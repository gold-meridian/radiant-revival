#ifndef TMLBUILD_HLSL
#define TMLBUILD_HLSL

#include "syntax.h"

#define GLOBAL_TIME CS_EXPR(global::Terraria.Main.GlobalTimeWrappedHourly)

#define SCREEN_SIZE_X CS_EXPR(global::Terraria.Main.screenWidth)
#define SCREEN_SIZE_Y CS_EXPR(global::Terraria.Main.screenHeight)
#define SCREEN_POSITION CS_EXPR(global::Terraria.Main.screenPosition)

#endif // TMLBUILD_HLSL