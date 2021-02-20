using UnityEngine;

namespace GoblinsInteractive.ProceduralIsometricMapGenerator
{
    public partial class ProceduralColliderSet : ScriptableObject
    {
        //naming schema: type(cliff/edge/stair) + half? + direction(top,right,bottom,left, double=top&right) + additional(WithXcutout, XSideOnly)
        public enum ColliderType
        {
            None,

            Central,

            EdgeDouble, // both top and right
            EdgeTop,
            EdgeRight,

            StairsTop, // bottom level < top level
            StairsTopBottomSideOnly,//used when there are 2 stairs next to each other
            StairsTopTopSideOnly,//used when there are 2 stairs next to each other
            StairsTopWithRightCliff,//when the top stair is left of a cliff - contains both bottom and top side of the stair
            StairsTopWithRightCliffTopSideOnly,//when the top stair is left of a cliff - contains only top side of the stair

            StairsRight, //left level< right level
            StairsRightBottomSideOnly,//used when there are 2 stairs next to each other
            StairsRightTopSideOnly,//used when there are 2 stairs next to each other        
            StairsRightWithCliffTop,
            StairsRightWithCliffTopTopSideOnly,

            //inverse stairs
            StairsBottom,//bottom level> top level
            StairsBottomBottomSideOnly,
            StairsBottomTopSideOnly,
            StairsBottomWithBottomCutout,
            StairsBottomWithBottomCutoutBottomSideOnly,

            //inverse stairs
            StairsLeft,//left level> right level
            StairsLeftBottomSideOnly,
            StairsLeftTopSideOnly,
            StairsLeftWithBottomCutout,
            StairsLeftWithBottomCutoutBottomSideOnly,

            CliffTop,
            CliffRight,
            CliffDouble,//both top and right
            CliffHalfTop,//points to left
            CliffHalfRight,//points to right
            CliffTopEdgeRight,
            CliffRightEdgeTop,
            CliffDoubleWithRightCutout,
            CliffDoubleWithLeftCutout,
            CliffDoubleWithDoubleCutout,
            CliffHalfTopEdgeRight,
            CliffHalfRightEdgeTop,
            CliffDoubleWithTopStairCutout,//stair is on the left tile
            CliffDoubleWithRightCutoutWithTopStairCutout,//stair is on the left tile
            CliffRightWithRightStairCutout,//stair is on the bottom tile
            CliffTopEdgeRightWithTopStairCutout,//stair is on the left tile
            CliffTopWithTopStairCutout,//stair is on the left tile
            CliffDoubleWithTopCutoutWithRightStairCutout,
            CliffDoubleWithCentral,
            CliffDoubleEdgeRightWithDoubleCutout,
            CliffDoubleEdgeTopWithDoubleCutout,
            CliffDoubleEdgeDoubleWithDoubleCutout,
            CliffDoubleEdgeRightWithRightCutout,
            CliffDoubleEdgeTopWithTopCutout,
            CliffHalfRightEdgeDouble,
            CliffHalfRightEdgeRight,
            CliffHalfTopEdgeDouble,
            CliffHalfTopEdgeTop,
            CliffRightWithCentral,
            CliffTopWithCentral,
            CliffRightEdgeTopWithRightStairCutout,
            CliffDoubleWithRightStairCutout,

            CliffDoubleWithTopCutoutWithCentral,
            CliffDoubleWithRightCutoutWithCentral
        }
    }
}