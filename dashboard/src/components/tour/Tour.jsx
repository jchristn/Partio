import React, { useEffect, useState, useCallback } from 'react';
import { useTour } from '../../context/TourContext';
import tourSteps from './tourSteps';
import './Tour.css';

export default function Tour() {
  const { isTourActive, currentTourStep, nextTourStep, prevTourStep, skipTour } = useTour();
  const [targetRect, setTargetRect] = useState(null);

  const step = tourSteps[currentTourStep];

  const updatePosition = useCallback(() => {
    if (!step || step.type === 'modal') {
      setTargetRect(null);
      return;
    }
    const el = document.querySelector(`[data-tour-id="${step.target}"]`);
    if (el) {
      setTargetRect(el.getBoundingClientRect());
    } else {
      setTargetRect(null);
    }
  }, [step]);

  useEffect(() => {
    if (!isTourActive) return;
    updatePosition();
    window.addEventListener('resize', updatePosition);
    window.addEventListener('scroll', updatePosition, true);
    return () => {
      window.removeEventListener('resize', updatePosition);
      window.removeEventListener('scroll', updatePosition, true);
    };
  }, [isTourActive, currentTourStep, updatePosition]);

  // Keyboard navigation
  useEffect(() => {
    if (!isTourActive) return;
    const handler = (e) => {
      if (e.key === 'Escape') skipTour();
      if (e.key === 'ArrowRight' || e.key === 'Enter') nextTourStep(tourSteps.length);
      if (e.key === 'ArrowLeft') prevTourStep();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [isTourActive, nextTourStep, prevTourStep, skipTour]);

  if (!isTourActive || !step) return null;

  // Welcome modal (step 0)
  if (step.type === 'modal') {
    return (
      <div className="tour-welcome-overlay">
        <div className="tour-welcome-modal">
          <div className="tour-welcome-icon">
            <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="var(--primary-color)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10" />
              <path d="M12 16v-4M12 8h.01" />
            </svg>
          </div>
          <div className="tour-tooltip-title">{step.title}</div>
          <div className="tour-tooltip-desc">{step.description}</div>
          <div className="tour-tooltip-footer">
            <button className="tour-btn-skip" onClick={skipTour}>Skip Tour</button>
            <button className="tour-btn-next" onClick={() => nextTourStep(tourSteps.length)}>Start Tour</button>
          </div>
        </div>
      </div>
    );
  }

  // Compute tooltip position
  const pad = 8;
  let tooltipStyle = {};
  let arrowClass = '';
  let arrowStyle = {};

  if (targetRect) {
    if (step.position === 'right') {
      tooltipStyle = {
        top: targetRect.top - 8,
        left: targetRect.right + pad + 12,
      };
      arrowClass = 'tour-arrow-left';
    } else if (step.position === 'bottom') {
      const tooltipWidth = 320;
      let left = targetRect.left;
      const maxLeft = window.innerWidth - tooltipWidth - 16;
      if (left > maxLeft) {
        const arrowLeft = targetRect.left + targetRect.width / 2 - left;
        arrowStyle = { left: Math.max(12, Math.min(arrowLeft, tooltipWidth - 12)) };
        left = maxLeft;
      }
      tooltipStyle = {
        top: targetRect.bottom + pad + 12,
        left,
      };
      arrowClass = 'tour-arrow-top';
    }
  }

  return (
    <>
      {/* Spotlight */}
      {targetRect && (
        <div
          className="tour-spotlight"
          style={{
            top: targetRect.top - 4,
            left: targetRect.left - 4,
            width: targetRect.width + 8,
            height: targetRect.height + 8,
          }}
        />
      )}
      {/* Click blocker behind tooltip */}
      <div
        style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          zIndex: 1099, background: 'transparent',
        }}
        onClick={(e) => e.stopPropagation()}
      />
      {/* Tooltip */}
      <div className="tour-tooltip" style={tooltipStyle}>
        {arrowClass && <div className={`tour-arrow ${arrowClass}`} style={arrowStyle} />}
        <div className="tour-tooltip-title">{step.title}</div>
        <div className="tour-tooltip-desc">{step.description}</div>
        <div className="tour-tooltip-footer">
          <span className="tour-tooltip-counter">
            {currentTourStep} of {tourSteps.length - 1}
          </span>
          <div className="tour-tooltip-buttons">
            <button className="tour-btn-skip" onClick={skipTour}>Skip</button>
            {currentTourStep > 1 && (
              <button className="tour-btn-prev" onClick={prevTourStep}>Prev</button>
            )}
            <button
              className="tour-btn-next"
              onClick={() => nextTourStep(tourSteps.length)}
            >
              {currentTourStep === tourSteps.length - 1 ? 'Finish' : 'Next'}
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
