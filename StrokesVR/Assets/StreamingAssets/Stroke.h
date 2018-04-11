#pragma once

#include "cinder/app/App.h"
#include "cinder/gl/gl.h"
#include "cinder/Log.h"
#include "cinder/Timeline.h"

using namespace ci;

namespace rph {

using StrokeRef = std::shared_ptr<class Stroke>;
    
class Stroke {
  public:
    struct Options {
        Options() {}
        float baseThickness = 3.0f;
        float minThickness = 2.0f;
        float maxThickness = 20.0f;
        float speedToThickness = 1.0f;
        int   lineCapPoints = 10;
    };
    
    static StrokeRef create(const Options &options = Options()) {
        return std::make_shared<Stroke>(options);
    }
    static StrokeRef create(const std::vector<vec2> &points, const Options &options = Options()) {
        return std::make_shared<Stroke>(points, options);
    }
    
    Stroke(const std::vector<vec2> &points, const Options &options = Options());
    Stroke(const Options &options = Options());
    
    void draw();
    void drawDebug();
    void update();
    
    void start(const vec2 &pos);
    void move(const vec2 &pos);
    void end();
    
    void animateIn(float duration, float delay = 0.0f);
    void animateOut(float duration, float delay = 0.0f);
    
    void setId(int id)                      { mId = id; }
    void setOptions(const Options &opts)    { mOpts = opts; mOptionsChanged = true; }
    
    bool   isDrawing() const                { return mDrawing; }
    int    getId() const                    { return mId; }
    float  getLength() const                { return mStrokeLength; }
    double getStartTime() const             { return mStartTime; }
    double getEndTime() const               { return mEndTime; }
    double getDuration() const              { return mEndTime - mStartTime; }
    const  Rectf &getBounds() const         { return mBounds; }
    
  private:
    void updateGeometry();
    void createMesh();
    
    float calcThickness(const vec2 &pos);
    
  private:
    const int   mVerticesPerMesh = 600;
    const int   mPointsPerMesh = mVerticesPerMesh / 2;
    const float mMinSegmentLength = 20.0f;
    
    bool mDrawing = false;
    bool mAnimating = false;
    bool mOptionsChanged = false;
    bool mGeometryChanged = false;
    
    int     mId = -1;
    int     mNumUsedVertices = 0;
    float   mStrokeLength = 0;
    double  mStartTime;
    double  mEndTime;
    Rectf   mBounds;
    
    Options mOpts;
    
    Anim<float> mStartPct = 0.0f;
    Anim<float> mEndPct = 1.0f;
    
    std::vector<vec2>   mInputPoints;
    std::vector<vec2>   mSampledPoints;
    std::vector<vec2>   mOffsets;
    std::vector<float>  mThickness;
    std::vector<ColorA> mDebugColors;
    
    std::vector<gl::BatchRef>   mBatches;
    std::vector<gl::VboMeshRef> mVboMeshes;
    
    gl::GlslProgRef mShader  = nullptr;
    
};
    
}