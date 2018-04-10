#include "Stroke.h"
#include "cinder/Rand.h"

namespace rph {
    
Stroke::Stroke(const Options &options)
{
    mOpts = options;
}
    
Stroke::Stroke(const std::vector<vec2> &points, const Options &options)
{
    mOpts = options;
    
    for (auto &point : points) {
        if (mInputPoints.empty()) {
            start(point);
        }
        else {
            move(point);
        }
    }
    end();
}

void Stroke::start(const vec2 &pos)
{
    mDrawing = true;
    mStartTime = app::getElapsedSeconds();
    mBounds = Rectf(pos, pos);
    move(pos);
}

void Stroke::move(const vec2 &pos)
{
    if (!mDrawing) return;
    
    if (mInputPoints.size() <= 2) {
        mInputPoints.push_back(pos);
    }
    else {
        vec2 newPos = lerp(mInputPoints.back(), pos, 0.35f);
        
        // calculate thickness
        float dist = glm::distance(newPos, mInputPoints.back());
        
        if (dist < 1.0) return;
        
//        float newThick = clamp(mOpts.baseThickness + dist * mOpts.speedToThickness, mOpts.minThickness, mOpts.maxThickness);
        float newThick = dist; // moved to updategeometry
        float prevThick = mThickness.empty() ? newThick : mThickness.back();
        float currThick = lerp(prevThick, newThick, 0.2);
        
        // get last 3 input points
        const vec2 &prev2 = mInputPoints.at(mInputPoints.size() - 2);
        const vec2 &prev1 = mInputPoints.at(mInputPoints.size() - 1);
        const vec2 &cur   = newPos;
    
        // create cubic bezier segment with inputs as control points
        Path2d path;
        path.moveTo((prev2 + prev1) / 2.0f);
        path.quadTo(prev1, (prev1 + cur) / 2.0f);
       
        // divide segment adaptatively depending on its length
        // save vertices and thickness
        float pathLength = path.calcLength();
        int divisions = ceil(pathLength / mMinSegmentLength);
        
        for (int i = 1; i <= divisions; i++) {
            float t = float(i) / float(divisions);
            float thick = lerp(prevThick, currThick, t);
            vec2 point = path.getPosition(t);
            vec2 norm  = glm::normalize(newPos - (mSampledPoints.size() ? mSampledPoints.back() : mInputPoints.back()));
            vec2 perp  = vec2(-norm.y, norm.x);
            
            mSampledPoints.push_back(point);
            mThickness.push_back(thick);
            mOffsets.push_back(perp);
            
            //CI_LOG_D("interp: " << count << " t: " << t);
            //CI_LOG_D("interp: " << count << " x: " << x << "/" << pathLength << " t: " << t);
            //CI_LOG_D("added point x: " << smthPoint << " thick: " << thick);
        }
        
        mGeometryChanged = true;
        mInputPoints.push_back(newPos);
        mBounds.include(newPos);
    }
}
    
void Stroke::end()
{
    if (!mDrawing) return;
    
    mDrawing = false;
    mEndTime = app::getElapsedSeconds();
    
    updateGeometry();
}
    
void Stroke::update()
{
    if (!mDrawing && !mOptionsChanged && !mGeometryChanged) return;
    
    mOptionsChanged = false;
    mGeometryChanged = false;
    
    updateGeometry();
}
    
    
void Stroke::updateGeometry()
{
    if (!mShader) {
        mShader = gl::getStockShader(gl::ShaderDef().color());
    }

    int numPoints = mSampledPoints.size();
   
    if (numPoints > 0) {
        mNumUsedVertices = 0;
        
        // create necessary meshes
        // account for one point overlap between meshes
        int extraPoints = numPoints / mPointsPerMesh;
        int meshesNeeded = 1 + (extraPoints + numPoints) / mPointsPerMesh;
        
        while (mVboMeshes.size() < meshesNeeded ) {
            createMesh();
        }
        
        // update vertex positions
        // (we should only need to do this for the last mesh)
        for (int i = 0; i < mVboMeshes.size(); i++) {
        
            int start = i * (mPointsPerMesh - 1);
            int end = glm::min(start + mPointsPerMesh, (int) mSampledPoints.size());
            
            auto vertPosIter = mVboMeshes.at(i)->mapAttrib2f(geom::POSITION);
            
            for (int j = start; j < end; j++) {
                float p = glm::min(j, numPoints - j - 1);
                float x = 1.0f - glm::clamp(p / float(mOpts.lineCapPoints), 0.0f, 1.0f);
                
                float lineCap = glm::sqrt(1 - pow(x, 2));
                float thick = clamp(mOpts.baseThickness + mThickness.at(j) * mOpts.speedToThickness, mOpts.minThickness, mOpts.maxThickness);
                
                *vertPosIter = mSampledPoints.at(j) - mOffsets.at(j) * lineCap * thick;
                ++vertPosIter;
                
                *vertPosIter = mSampledPoints.at(j) + mOffsets.at(j) * lineCap * thick;
                ++vertPosIter;
                
                mNumUsedVertices += 2;
            }
            
            vertPosIter.unmap();
        }
    }
}
    
void Stroke::createMesh()
{
    // create a new mesh with all vertices collapsed
    std::vector<vec2> vertices(mVerticesPerMesh);
    
    auto layout = gl::VboMesh::Layout().usage(GL_DYNAMIC_DRAW).attrib(geom::Attrib::POSITION, 2);
    
    auto mesh = gl::VboMesh::create( mVerticesPerMesh, GL_TRIANGLE_STRIP, { layout } );
    mesh->bufferAttrib( geom::POSITION, sizeof(vec2) * vertices.size(), vertices.data() );
    
    mVboMeshes.push_back(mesh);
    mBatches.push_back(gl::Batch::create(mesh, mShader));
    
    mDebugColors.emplace_back(ColorA(CM_HSV, randFloat(), 1.0, 1.0, 0.5));
}
    
void Stroke::draw()
{
    int totalVertices = float(mNumUsedVertices) * mEndPct();
    
    for (int i = 0; i < mBatches.size(); i++) {
        int count = glm::min(mVerticesPerMesh , glm::max(totalVertices - i * mVerticesPerMesh, 0));
        int start = glm::max(int(float(totalVertices) * (mStartPct())) - i * mVerticesPerMesh, 0);
        mBatches[i]->draw(start, count - start);
    }

	//probably do this as a batch instead
	float maxPoints = 10.0f;
	if (mInputPoints.size() < maxPoints) {
		float size = 2; // *((maxPoints / mInputPoints.size()) / maxPoints);
		gl::drawSolidEllipse(mInputPoints.back(), size, size, 10);
	}
}

void Stroke::drawDebug()
{
    // draw bounding box
    gl::ScopedColor gray(Color::gray(0.75));
    gl::drawStrokedRect(mBounds);
    
    // draw mesh wireframe
    gl::enableWireframe();
    int totalVertices = float(mNumUsedVertices) * mEndPct();
    
    for (int i = 0; i < mBatches.size(); i++) {
        int count = glm::min(mVerticesPerMesh , glm::max(totalVertices - i * mVerticesPerMesh, 0));
        int start = glm::max(int(float(totalVertices) * (mStartPct())) - i * mVerticesPerMesh, 0);
        // int start = glm::max(int(float(totalVertices) * (mStartPct())) - i * count, 0);
        
        gl::ScopedColor col(mDebugColors[i]);
        mBatches[i]->draw(start, count - start);
    }
    gl::disableWireframe();
    
    // draw sampled points
    auto points = gl::VertBatch( GL_POINTS );
    for (auto &pos : mSampledPoints) {
        points.vertex(pos);
    }
    
    gl::pointSize(3.0);
    gl::ScopedColor white(1, 1, 1);
    points.draw();
    gl::pointSize(1.0);
}
    
void Stroke::animateIn(float duration, float delay)
{
    if (mDrawing) return;
    
    mAnimating = true;
    mStartPct.stop();
    mEndPct.stop();
    mStartPct = 0.0f;
    mEndPct = 0.0f;
    
    app::timeline().apply(&mEndPct, 1.0f, duration).delay(delay).finishFn([this] { mAnimating = false; });
}

void Stroke::animateOut(float duration, float delay)
{
    if (mDrawing) return;
    
    mAnimating = true;
    mStartPct.stop();
    mEndPct.stop();
    mStartPct = 0.0f;
    mEndPct = 1.0f;
    
    app::timeline().apply(&mStartPct, 1.0f, duration).delay(delay).finishFn([this] { mAnimating = false; });
}

} // namespace rph