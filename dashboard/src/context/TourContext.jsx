import React, { createContext, useContext, useState, useCallback, useEffect } from 'react';

const TourContext = createContext(null);

export function TourProvider({ children }) {
  const [isTourActive, setIsTourActive] = useState(false);
  const [currentTourStep, setCurrentTourStep] = useState(0);
  const [isWizardActive, setIsWizardActive] = useState(false);
  const [currentWizardStep, setCurrentWizardStep] = useState(() => {
    const saved = localStorage.getItem('partio_wizardCurrentStep');
    return saved ? parseInt(saved, 10) : 0;
  });
  const [hasCompletedTour, setHasCompletedTour] = useState(
    () => localStorage.getItem('partio_hasCompletedTour') === 'true'
  );
  const [hasCompletedSetup, setHasCompletedSetup] = useState(
    () => localStorage.getItem('partio_hasCompletedSetup') === 'true'
  );

  // Auto-start tour on first visit
  useEffect(() => {
    if (!hasCompletedTour) {
      setIsTourActive(true);
      setCurrentTourStep(0);
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const startTour = useCallback(() => {
    setCurrentTourStep(0);
    setIsTourActive(true);
  }, []);

  const nextTourStep = useCallback((totalSteps) => {
    setCurrentTourStep(prev => {
      if (prev + 1 >= totalSteps) {
        setIsTourActive(false);
        setHasCompletedTour(true);
        localStorage.setItem('partio_hasCompletedTour', 'true');
        // Auto-start wizard if setup not completed
        if (localStorage.getItem('partio_hasCompletedSetup') !== 'true') {
          setTimeout(() => {
            setIsWizardActive(true);
          }, 300);
        }
        return 0;
      }
      return prev + 1;
    });
  }, []);

  const prevTourStep = useCallback(() => {
    setCurrentTourStep(prev => Math.max(0, prev - 1));
  }, []);

  const skipTour = useCallback(() => {
    setIsTourActive(false);
    setHasCompletedTour(true);
    localStorage.setItem('partio_hasCompletedTour', 'true');
    // Auto-start wizard if setup not completed
    if (!hasCompletedSetup) {
      setTimeout(() => {
        setIsWizardActive(true);
      }, 300);
    }
  }, [hasCompletedSetup]);

  const startWizard = useCallback(() => {
    const saved = localStorage.getItem('partio_wizardCurrentStep');
    setCurrentWizardStep(saved ? parseInt(saved, 10) : 0);
    setIsWizardActive(true);
  }, []);

  const nextWizardStep = useCallback((totalSteps) => {
    setCurrentWizardStep(prev => {
      const next = Math.min(prev + 1, totalSteps - 1);
      localStorage.setItem('partio_wizardCurrentStep', String(next));
      return next;
    });
  }, []);

  const prevWizardStep = useCallback(() => {
    setCurrentWizardStep(prev => {
      const next = Math.max(0, prev - 1);
      localStorage.setItem('partio_wizardCurrentStep', String(next));
      return next;
    });
  }, []);

  const skipWizard = useCallback(() => {
    setIsWizardActive(false);
  }, []);

  const completeWizard = useCallback(() => {
    setIsWizardActive(false);
    setHasCompletedSetup(true);
    localStorage.setItem('partio_hasCompletedSetup', 'true');
    localStorage.removeItem('partio_wizardCurrentStep');
  }, []);

  const value = {
    isTourActive, currentTourStep,
    isWizardActive, currentWizardStep,
    hasCompletedTour, hasCompletedSetup,
    startTour, nextTourStep, prevTourStep, skipTour,
    startWizard, nextWizardStep, prevWizardStep, skipWizard, completeWizard,
  };

  return <TourContext.Provider value={value}>{children}</TourContext.Provider>;
}

export function useTour() {
  const ctx = useContext(TourContext);
  if (!ctx) throw new Error('useTour must be used within TourProvider');
  return ctx;
}
