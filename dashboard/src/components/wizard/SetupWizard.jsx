import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useTour } from '../../context/TourContext';
import wizardSteps from './wizardSteps.jsx';
import './SetupWizard.css';

export default function SetupWizard() {
  const {
    isWizardActive, currentWizardStep,
    nextWizardStep, prevWizardStep, skipWizard, completeWizard,
  } = useTour();
  const navigate = useNavigate();

  if (!isWizardActive) return null;

  const step = wizardSteps[currentWizardStep];
  const isLast = currentWizardStep === wizardSteps.length - 1;

  const handleGoTo = () => {
    navigate(step.navigateTo);
    skipWizard();
  };

  return (
    <div className="wizard-overlay" onClick={skipWizard}>
      <div className="wizard-content" onClick={e => e.stopPropagation()}>
        {/* Header */}
        <div className="wizard-header">
          <h3>Setup Wizard</h3>
          <button className="wizard-close" onClick={skipWizard}>&times;</button>
        </div>

        {/* Progress bar */}
        <div className="wizard-progress">
          {wizardSteps.map((_, i) => (
            <div className="wizard-progress-step" key={i}>
              <div
                className={`wizard-progress-circle${
                  i === currentWizardStep ? ' active' : ''
                }${i < currentWizardStep ? ' completed' : ''}`}
              >
                {i < currentWizardStep ? (
                  <svg width="14" height="14" viewBox="0 0 20 20" fill="currentColor">
                    <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                  </svg>
                ) : (
                  i + 1
                )}
              </div>
              {i < wizardSteps.length - 1 && (
                <div className={`wizard-progress-line${i < currentWizardStep ? ' completed' : ''}`} />
              )}
            </div>
          ))}
        </div>

        {/* Step content */}
        <div className="wizard-body">
          <div className="wizard-step-icon">{step.icon}</div>
          <div className="wizard-step-title">{step.title}</div>
          <div className="wizard-step-desc">{step.description}</div>
          <div className="wizard-step-tip">
            <strong>Tip: </strong>{step.tip}
          </div>
          <button className="wizard-goto-btn" onClick={handleGoTo}>
            {step.buttonLabel}
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M10.293 3.293a1 1 0 011.414 0l6 6a1 1 0 010 1.414l-6 6a1 1 0 01-1.414-1.414L14.586 11H3a1 1 0 110-2h11.586l-4.293-4.293a1 1 0 010-1.414z" clipRule="evenodd" />
            </svg>
          </button>
        </div>

        {/* Footer */}
        <div className="wizard-footer">
          <span className="wizard-footer-counter">
            Step {currentWizardStep + 1} of {wizardSteps.length}
          </span>
          <div className="wizard-footer-buttons">
            <button className="wizard-btn-skip" onClick={skipWizard}>Close</button>
            {currentWizardStep > 0 && (
              <button className="wizard-btn-prev" onClick={prevWizardStep}>Prev</button>
            )}
            {isLast ? (
              <button className="wizard-btn-complete" onClick={completeWizard}>
                Complete Setup
              </button>
            ) : (
              <button className="wizard-btn-next" onClick={() => nextWizardStep(wizardSteps.length)}>
                Next
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
