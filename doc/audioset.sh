#!/bin/bash

# For the audio set of the Luckfox board
amixer cset name='ADC ALC Left Volume' 26
amixer cset name='ADC ALC Right Volume' 6
amixer cset name='ADC Digital Left Volume' 195
amixer cset name='ADC Digital Right Volume' 195
amixer cset name='ADC MIC Left Gain' 3
amixer cset name='ADC MICBIAS Voltage' 'VREFx0_975'
amixer cset name='ADC Mode' 'SingadcL'
amixer cset name='ADC HPF Cut-off' 'On'
amixer cset name='DAC LINEOUT Volume' 20